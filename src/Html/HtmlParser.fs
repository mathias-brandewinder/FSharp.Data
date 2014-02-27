﻿namespace FSharp.Data.Runtime

open System
open System.IO
open System.Text
open FSharp.Data

#if INTERACTIVE
open FSharp.Data.Runtime
#endif

module private TextParser = 

    let (|NullChar|_|) (c : Char) =
        if (c |> int) = 0 then Some c else None

    let (|EndOfFile|_|) (c : Char) =
        let value = c |> int
        if (value = -1 || value = 65535) then Some c else None

    let (|UpperAtoZ|_|) (c : Char) =
        if Char.IsUpper(c) then Some c else None

    let (|LowerAtoZ|_|) (c : Char) =
        if Char.IsLower(c) then Some c else None

    let (|Number|_|) (c : Char) =
        if Char.IsNumber(c) then Some c else None

    let (|Symbol|_|) (c : Char) =
        if Char.IsPunctuation(c) then Some c else None

    let (|Whitespace|_|) (c : Char) =
        if Char.IsWhiteSpace(c) then Some c else None

    let (|LetterDigit|_|) = function
        | LowerAtoZ c -> Some c
        | Number c -> Some c
        | UpperAtoZ c -> Some (Char.ToLower(c))
        | _ -> None

    let (|Letter|_|) = function
        | LowerAtoZ c -> Some c
        | UpperAtoZ c -> Some (Char.ToLower(c))
        | _ -> None

    let (|LetterDigitSymbol|_|) = function
        | LowerAtoZ c -> Some c
        | Number c -> Some c
        | UpperAtoZ c -> Some (Char.ToLower(c))
        | Symbol c -> Some c
        | _ -> None

type HtmlAttribute = | HtmlAttribute of string * string
    with
        member x.Name = 
            match x with
            | HtmlAttribute(name,_) -> name
        member x.Value = 
            match x with
            | HtmlAttribute(_,value) -> value

type HtmlToken =
    | DocType of string
    | Tag of bool * string * HtmlAttribute list
    | TagEnd of string
    | Text of string
    | CharRef of string
    | Script of string
    | Style of string
    | Comment of string
    | EOF

type HtmlElement =
    | HtmlElement of string * HtmlAttribute list * HtmlElement list
    | HtmlCharRef of string
    | HtmlText of string
    | HtmlScript of string
    | HtmlStyle of string
    | HtmlComment of string

type HtmlDocument = | HtmlDocument of string * HtmlElement list

module HtmlParser =

    module private Helpers = 
        
        type System.IO.StreamReader with
       
            static member NullChar = Convert.ToChar(0x0)
            member x.PeekChar() = x.Peek() |> char
            member x.ReadChar() = x.Read() |> char
            member x.ReadNChar(n) = 
                let buffer = Array.zeroCreate n
                x.ReadBlock(buffer, 0, n) |> ignore
                String(buffer)
    
        type CharList = {
            Contents : char list ref
        }
        with
            static member Empty = { Contents = ref [] }
            override x.ToString() = String(!x.Contents |> List.rev |> Seq.toArray)
            member x.Cons(c) = 
                x.Contents := c :: !x.Contents
            member x.Length = x.Contents.Value.Length
            member x.Clear() = x.Contents := []

        type InsertionMode = 
            | ScriptMode
            | StyleMode
            | DefaultMode
            | CharRefMode
            | CommentMode
            | DocTypeMode
            with
                override x.ToString() =
                    match x with
                    | ScriptMode -> "script"
                    | StyleMode -> "style"
                    | DefaultMode -> "default"
                    | CharRefMode -> "charref"
                    | CommentMode -> "comment"
                    | DocTypeMode -> "doctype"
    
        type HtmlState = {
            Attributes : (CharList * CharList) list ref
            CurrentTag : CharList ref
            Content : CharList ref
            InsertionMode : InsertionMode ref
            Reader : StreamReader
        }
        with
            static member Create(reader:StreamReader) = {
                Attributes = ref []
                CurrentTag = ref CharList.Empty
                Content = ref CharList.Empty
                InsertionMode = ref DefaultMode
                Reader = reader
            }

            member x.Pop() = x.Reader.Read() |> ignore
            member x.Peek() = x.Reader.PeekChar()
    
            member x.NewAttribute() = x.Attributes := (CharList.Empty, CharList.Empty) :: (!x.Attributes)
    
            member x.ConsAttrName() =
                match !x.Attributes with
                | [] -> x.NewAttribute(); x.ConsAttrName()
                | (h,_) :: _ -> h.Cons(x.Reader.ReadChar())
    
            member x.CurrentTagName() = 
                match (!(!x.CurrentTag).Contents) with
                | [] ->  String.Empty
                | h :: _ -> h.ToString()
    
            member x.CurrentAttrName() = 
                match !x.Attributes with
                | [] ->  String.Empty
                | (h,_) :: _ -> h.ToString() 
    
            member x.ConsAttrValue() =
                match !x.Attributes with
                | [] -> x.NewAttribute(); x.ConsAttrValue()
                | (_,h) :: _ -> h.Cons(x.Reader.ReadChar())
    
            member x.GetAttributes() = 
                !x.Attributes |> List.choose (fun (key,value) -> 
                                                if key.Length > 0
                                                then Some <| HtmlAttribute(key.ToString(), value.ToString())
                                                else None
                                              )
    
            member x.EmitSelfClosingTag() = 
                let name = (!x.CurrentTag).ToString()
                let result = Tag(true, name, x.GetAttributes()) 
                x.CurrentTag := CharList.Empty
                x.InsertionMode := DefaultMode
                x.Attributes := []
                result 
    
            member x.EmitTag(isEnd) =
                let name = (!x.CurrentTag).ToString()
                let isVoid (name:string) = 
                    match name.Trim().ToLower() with
                    | "area" | "base" | "br" | "col" | "embed"| "hr" | "img" | "input" | "keygen" | "link" | "menuitem" | "meta" | "param" 
                    | "source" | "track" | "wbr" -> true
                    | _ -> false
                let result = 
                    if isEnd
                    then TagEnd(name)
                    else Tag((isVoid name), name, x.GetAttributes()) 
                x.CurrentTag := CharList.Empty
                x.InsertionMode :=
                    match isEnd, name with
                    | false, "script" -> ScriptMode
                    | false, "style" -> StyleMode
                    | _, _ -> DefaultMode
                x.Attributes := []
                result
    
            member x.Emit() = 
                let result : HtmlToken = 
                    match !x.InsertionMode with
                    | DefaultMode -> Text
                    | ScriptMode -> Script
                    | StyleMode -> Style
                    | CharRefMode -> CharRef
                    | CommentMode -> Comment
                    | DocTypeMode -> DocType
                    <| ((!x.Content).ToString())
                x.Content := CharList.Empty
                x.InsertionMode := DefaultMode
                result
    
            member x.Cons() = (!x.Content).Cons(x.Reader.ReadChar())
            member x.ConsTag() = (!x.CurrentTag).Cons(x.Reader.ReadChar())
            member x.ClearContent() = 
                (!x.Content).Clear()
    
    open Helpers
    
    //Tokenises a stream into a sequence of HTML tokens. 
    let private tokenise (sr : #StreamReader) =
        let state = HtmlState.Create(sr)
        let rec data (state:HtmlState) =
            match state.Reader.PeekChar() with
            | '<' when (!state.Content).Length > 0 -> state.Emit()
            | '<' -> state.Pop(); tagOpen state
            | TextParser.EndOfFile _ -> EOF
            | '&' -> 
                state.InsertionMode := CharRefMode
                charRef state
            | _ ->
                match !state.InsertionMode with
                | ScriptMode ->  state.Cons(); script state
                | StyleMode -> state.Cons(); script state
                | DefaultMode -> state.Cons(); data state
                | CharRefMode -> charRef state
                | DocTypeMode -> docType state
                | CommentMode -> bogusComment state
        and script state = 
            match state.Peek() with
            | '<' -> state.Pop(); scriptLessThanSign state
            | TextParser.EndOfFile _ -> data state
            | _ -> state.Cons(); script state
        and scriptLessThanSign state =
            match state.Peek() with
            | '/' -> state.Pop(); (!state.Content).Clear(); scriptEndTagOpen state
            | _ -> state.Cons(); script state
        and scriptEndTagOpen state = 
            match state.Peek() with
            | TextParser.Letter _ -> state.ConsTag(); scriptEndTagName state;
            | _ -> script state
        and scriptEndTagName state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); scriptEndTagName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop();  state.EmitTag(true); 
            | TextParser.Letter _ -> state.ConsTag(); scriptEndTagName state;
            | _ -> state.ConsTag(); scriptEndTagName state;
        and charRef state = 
            match state.Peek() with
            | ';' -> state.Cons(); state.Emit()
            | '<' -> state.Pop(); state.Emit()
            | _ -> state.Cons(); charRef state
        and tagOpen state =
            match state.Peek() with
            | '!' ->  state.Pop(); docType state
            | '/'  -> state.Pop(); endTagOpen state
            | '?' -> state.Pop(); bogusComment state
            | TextParser.Letter _ -> state.ConsTag(); tagName false state
            | _ -> data state
        and docType state =
            match state.Peek() with
            | '>' -> 
                state.Pop(); 
                state.InsertionMode := DocTypeMode
                state.Emit()
            | _ -> state.Cons(); docType state
        and bogusComment state = 
            match state.Peek() with
            | '>' -> 
                state.Pop();
                state.InsertionMode := CommentMode 
                state.Emit()
            | TextParser.EndOfFile _ -> 
                state.InsertionMode := CommentMode 
                state.Emit();
            | _ -> state.Cons(); bogusComment state
        and tagName isEndTag state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(isEndTag)
            | TextParser.EndOfFile _ -> data state
            | _ -> state.ConsTag(); tagName isEndTag state
        and selfClosingStartTag state = 
            match state.Peek() with
            | '>' -> state.Pop(); state.EmitSelfClosingTag();
            | TextParser.EndOfFile _ -> data state
            | _ -> beforeAttributeName state
        and endTagOpen state = 
            match state.Peek() with
            | TextParser.Letter _ -> state.ConsTag(); tagName true state
            | TextParser.EndOfFile _ -> data state
            | '>' -> state.Pop(); data state
            | _ -> bogusComment state
        and beforeAttributeName state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> selfClosingStartTag state
            | TextParser.EndOfFile _ -> data state
            | _ -> attributeName state
        and attributeName state =
            match state.Peek() with
            | '\'' | '"'  -> state.Pop(); attributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '=' -> state.Pop(); beforeAttributeValue state;
            | '>' -> state.Pop(); state.EmitTag(false)
            | TextParser.LetterDigit _ -> state.ConsAttrName(); attributeName state;
            | TextParser.Whitespace _ -> state.ConsAttrName(); attributeName state;
            | _ -> state.ConsAttrName(); tagOpen state
        and beforeAttributeValue state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); beforeAttributeValue state
            | '"' -> state.Pop(); attributeValueQuoted state
            | '&' -> state.Pop(); attributeValueUnquoted state
            | '\'' -> state.Pop(); attributeValueSingleQuoted state
            | '>' -> state.Pop(); state.EmitTag(false);
            | _ -> state.ConsAttrValue(); attributeValueUnquoted state;
        and attributeValueUnquoted state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); state.NewAttribute(); attributeName state
            | '>' -> state.Pop(); state.EmitTag(false)
            | _ -> state.ConsAttrValue(); attributeValueUnquoted state
        and attributeValueSingleQuoted state = 
            match state.Peek() with
            | '\'' -> state.Pop(); afterAttributeValueQuoted state
            | '/' -> state.EmitSelfClosingTag()
            | _ -> state.ConsAttrValue(); attributeValueQuoted state
        and attributeValueQuoted state = 
            match state.Peek() with
            | '"' -> state.Pop(); afterAttributeValueQuoted state
            | _ -> state.ConsAttrValue(); attributeValueQuoted state
        and afterAttributeValueQuoted state = 
            match state.Peek() with
            | TextParser.Whitespace _ -> state.Pop(); state.NewAttribute(); attributeName state
            | '/' -> state.Pop(); selfClosingStartTag state
            | '>' -> state.Pop(); state.EmitTag(false)
            | TextParser.EndOfFile _ -> data state
            | _ -> attributeName state
        
        [
           while state.Reader.EndOfStream |> not do
               yield data state
        ]
    
    let parseStreamReader sr =
        let rec parse' docType elements tokens =
            match tokens with
            | DocType(dt) :: rest -> parse' dt elements rest
            | Tag(true, name, attributes) :: rest ->
               let e = HtmlElement(name.ToLower(), attributes, [])
               parse' docType (e :: elements) rest
            | Tag(false, name, attributes) :: rest ->
                let dt, tokens, content = parse' docType [] rest
                let e = HtmlElement(name.ToLower(), attributes, content)
                parse' dt (e :: elements) tokens
            | TagEnd(_) :: rest -> docType, rest, (elements |> List.rev)
            | Text(cont) :: rest -> 
                let text = cont.Trim()
                if String.IsNullOrEmpty(text) || String.IsNullOrWhiteSpace(text)
                then parse' docType (elements) rest
                else parse' docType (HtmlText(text) :: elements) rest
            | Script(cont) :: rest -> parse' docType (HtmlScript(cont.Trim()) :: elements) rest
            | Comment(cont) :: rest -> parse' docType (HtmlComment(cont.Trim()) :: elements) rest
            | CharRef(cont) :: rest -> parse' docType (HtmlCharRef(cont.Trim()) :: elements) rest
            | Style(cont) :: rest -> parse' docType (HtmlStyle(cont.Trim()) :: elements) rest
            | EOF :: _ -> docType, [], (elements |> List.rev)
            | [] -> docType, [], (elements |> List.rev)
        let docType, _, elements = tokenise sr |> parse' "" []
        HtmlDocument(docType, elements)

    let parseString (enc:Encoding) (str:string) = 
        use ms = new MemoryStream(enc.GetBytes(str))
        use sr = new StreamReader(ms)
        parseStreamReader sr

    let parseAsync (sampleOrUri:string) = async { 
        match Uri.TryCreate(sampleOrUri, UriKind.Absolute) with
        | true, uri ->
            let! stream = 
                match IO.isWeb uri with
                | true -> async { 
                            let! stream =  Http.InnerRequest(uri.OriginalString, fun _ _ _ _ _ _ stream -> stream) 
                            return stream :> Stream
                          }
                | false -> async {
#if FX_NO_LOCAL_FILESYSTEM
                        failwith "Only web locations are supported"
                        return Unchecked.defaultof<_>
#else
                        let path = uri.OriginalString.Replace(Uri.UriSchemeFile + "://", "")
                        return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) :> Stream 
#endif
                  }
            use sr = new StreamReader(stream)
            let body = parseStreamReader sr
            return Some(body) 
        | false, _ ->
            try
                return Some <| parseString Encoding.UTF8 sampleOrUri
            with _ -> 
                return None 
        }

    let parse sampleOrUri = 
        parseAsync sampleOrUri |> Async.RunSynchronously