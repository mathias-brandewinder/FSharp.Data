﻿// --------------------------------------------------------------------------------------
// Implements type inference for XML
// --------------------------------------------------------------------------------------

module ProviderImplementation.XmlInference

open System
open System.Xml.Linq
open ProviderImplementation
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralInference
open FSharp.Data.Runtime.StructuralTypes

// The type of XML element is always a non-optional record with a field
// for every attribute. If it has some content, then it also 
// contains a special field named "" which is either a collection
// (of other records etc.) or a primitive with the type of the content

/// Generates record fields for all attributes
let private getAttributes cultureInfo (element:XElement) =
  [ for attr in element.Attributes() do
      if attr.Name.Namespace.NamespaceName <> "http://www.w3.org/2000/xmlns/" then
        yield { Name = attr.Name.ToString()
                Type = getInferedTypeFromString cultureInfo attr.Value None } ]

let getInferedTypeFromValue cultureInfo (element:XElement) =
    let value = element.Value
    let typ = getInferedTypeFromString cultureInfo value None
    match typ with
    | InferedType.Primitive(t, _, optional) when t = typeof<string> && value.TrimStart().StartsWith "{" ->
        try        
            match JsonValue.Parse value with
            | JsonValue.Object _ as json -> 
                let jsonType = json |> JsonInference.inferType cultureInfo element.Name.LocalName
                InferedType.Json(jsonType, optional)
            | _ -> typ
        with _ -> typ
    | _ -> typ

/// Infers type for the element, unifying nodes of the same name
/// accross the entire document (we first get information based
/// on just attributes and then use a fixed point)
let inferGlobalType cultureInfo allowEmptyValues (element:XElement) =

  // Initial state contains types with attributes but all 
  // children are ignored (bodies are based on just body values)
  let initialTypes =
    element.Document.Descendants() 
    |> Seq.groupBy (fun el -> el.Name)
    |> Seq.map (fun (name, elements) ->
        // Get attributes for all `name` named elements 
        let attributes =
          elements
          |> Seq.map (getAttributes cultureInfo)
          |> Seq.reduce (unionRecordTypes allowEmptyValues)

        // Get type of body based on primitive values only
        let bodyType = 
          [ for e in elements do
              if not e.HasElements && not (String.IsNullOrEmpty(e.Value)) then
                yield getInferedTypeFromValue cultureInfo e ]
          |> Seq.fold (subtypeInfered allowEmptyValues) InferedType.Top
        let body = { Name = ""
                     Type = bodyType }

        let record = InferedType.Record(Some(name.ToString()), body::attributes, false)
        name.ToString(), (elements, record) )
    |> Map.ofSeq

  /// Updates the types representing body in a given assignment
  /// (This is done repeatedly until we reach a fixed point)
  let assignment = initialTypes
  let mutable changed = true
  while changed do 
    changed <- false
    for KeyValue(_, value) in assignment do
      match value with 
      | elements, InferedType.Record(Some _name, body::_attributes, false) -> 
          if body.Name <> "" then failwith "inferGlobalType: Assumed body element first"
          let childrenType = [ for e in elements -> 
                                 inferCollectionType allowEmptyValues [ for e in e.Elements() -> assignment.[e.Name.ToString()] |> snd ] ]
                             |> List.fold (subtypeInfered allowEmptyValues) InferedType.Top
          let bodyType =
              match childrenType with
              | InferedType.Collection (EmptyMap () _) -> body.Type
              | childrenType -> subtypeInfered allowEmptyValues childrenType body.Type
          changed <- changed || body.Type <> bodyType
          body.Type <- bodyType
      | _ -> failwith "inferGlobalType: Expected record type with a name"

  assignment.[element.Name.ToString()] |> snd

/// Get information about type locally (the type of children is infered
/// recursively, so same elements in different positions have different types)
let rec inferLocalType cultureInfo allowEmptyValues (element:XElement) = 
  let props = 
    [ // Generate record fields for attributes
      yield! getAttributes cultureInfo element
      
      // If it has children, add collection content
      let children = element.Elements()
      if Seq.length children > 0 then
        let collection = inferCollectionType allowEmptyValues (Seq.map (inferLocalType cultureInfo allowEmptyValues) children)
        yield { Name = ""
                Type = collection } 

      // If it has value, add primitive content
      elif not (String.IsNullOrEmpty element.Value) then
        let primitive = getInferedTypeFromValue cultureInfo element
        yield { Name = ""
                Type = primitive } ]

  InferedType.Record(Some(element.Name.ToString()), props, false)

/// A type is infered either using `inferLocalType` which only looks
/// at immediate children or using `inferGlobalType` which unifies nodes
/// of the same name in the entire document
let inferType cultureInfo allowEmptyValues globalInference (element:XElement) = 
  if globalInference then inferGlobalType cultureInfo allowEmptyValues element
  else inferLocalType cultureInfo allowEmptyValues element
