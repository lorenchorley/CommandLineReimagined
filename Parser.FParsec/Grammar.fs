/// The command line grammar, as parser combinators.
///
/// This is a direct translation of CommandLineGrammar.grm. The rules appear in the same
/// order and under the same names as the BNF, so the two can be read side by side.
///
/// Two differences from the GOLD version are deliberate:
///   - Every production builds a tree. The table-driven parser left roughly a third of
///     the grammar throwing NotImplementedException, so component tags, multi-argument
///     function calls, tags as values and property tag lists parsed but could not be
///     used.
///   - The three-quote string form works. <Constant> in the .grm lists StringLiteral3
///     twice and never StringLiteral4, so the documented form was unreachable.
module CommandLineReimagined.Parsing.Grammar

open System
open FParsec
open Commands.Parser.SemanticTree

type private P<'a> = Parser<'a, unit>

// ----------------------------------------------------------------- Whitespace

// The grammar is line oriented: a command line is one line, and newlines inside a
// string are part of the string rather than separators.
let private isInlineSpace c = c = ' ' || c = '\t'
let private ws: P<unit> = skipManySatisfy isInlineSpace
let private ws1: P<unit> = skipMany1Satisfy isInlineSpace

let private tok (p: P<'a>) : P<'a> = p .>> ws
let private sym (c: char) : P<unit> = skipChar c .>> ws
let private symStr (s: string) : P<unit> = skipString s .>> ws

// ----------------------------------------------------------------- Sets
// {IdentifierCharacter} = {AlphaNumeric} + [_ and a list of accented letters]

let private accented =
    "éèàäëïöüùçâêîôûÇÄÅÉæÆÖÜøØƒáíóúñÑÁÂÀãÃðÐÊËÈiÍÎÏÌÓßÔÒõÕµþÞÚÛÙýÝ"

let private isIdentifierChar (c: char) =
    (c >= 'a' && c <= 'z')
    || (c >= 'A' && c <= 'Z')
    || (c >= '0' && c <= '9')
    || c = '_'
    || accented.IndexOf c >= 0

/// Identifier = {IdentifierCharacter}+
let private identifierText: P<string> = many1SatisfyL isIdentifierChar "identifier"

/// <summary>A command's name: identifiers joined by single hyphens.</summary>
/// <remarks>
/// Decision 0022. `save-view`, and in Phase 6 `from-xml` and `to-csv`, are two words
/// that name one command, and a shell that cannot spell them would have to run them
/// together. The hyphen has to be adjacent on both sides, which is what keeps `ls -l`
/// a command and a flag and `echo -5` a command and a negative number: a space before
/// the hyphen ends the name.
/// </remarks>
let private commandNameText: P<string> =
    identifierText .>>. many (attempt (pchar '-' >>. identifierText))
    |>> fun (first, rest) -> first :: rest |> String.concat "-"

// A bare word: an unquoted command argument that is not a plain identifier, such as
// `notes.txt`, `../docs`, `C:\\Users` or `https://host/path`. The .grm sketched this as
// the commented-out {BareStringCharacter} set and never finished it, so every file name
// with a dot needed quotes.
// `*` is a word character because `like` takes a glob: decision 0007's point was that
// a notation you have to reach for a shift key to write is the wrong notation on a
// phone, and `where $row.name like "*.txt"` is exactly that. Nothing else in the
// grammar uses `*`, so there is nothing for it to collide with.
let private isWordChar (c: char) =
    isIdentifierChar c || ".\\/:~+@%-*".IndexOf c >= 0

// Decision 0050: `@` starts a word too, so a column `pick` answers, such as `@tag`, is
// written as it is shown: `select @tag`. Nothing else in the grammar starts with `@`;
// a member written `.@tag` is read by `memberName` before a word is looked for.
let private isWordStart (c: char) =
    isIdentifierChar c || ".\\/~*@".IndexOf c >= 0

// Decision 0007: `/>` and `/}` are delimiters, so a `/` belongs to the word it is in
// only when what follows is not one of those two brackets. That is what lets
// `<file path=documents/notes.txt/>` parse: the slashes inside the path join the word
// and the final one closes the tag. Deciding it by lookahead rather than by position
// means the same word parser serves both inside a tag and outside one, instead of the
// two dialects the previous rule needed.
let private wordSlash: P<char> = attempt (pchar '/' .>> notFollowedBy (anyOf ">}"))

// Decision 0007 again: a `-` in front of a digit starts a number rather than a flag,
// so `echo -5` writes minus five. Only in front of a digit, so `-x` is still a flag.
let private wordMinus: P<char> = attempt (pchar '-' .>> followedBy digit)

let private wordStartChar: P<char> =
    choice [ satisfy (fun c -> isWordStart c && c <> '/')
             wordSlash
             wordMinus ]

let private wordChar: P<char> =
    choice [ satisfy (fun c -> isWordChar c && c <> '/')
             wordSlash ]

/// Decision 0019: the word operators and the recovery keywords are never bare words,
/// in any position. The match is exact, so `equals`, `eq.txt` and `Eq` are ordinary
/// words and only the bare word itself is taken.
let reservedWords =
    [ "and"; "or"; "not"; "eq"; "ne"; "gt"; "ge"; "lt"; "le"; "like"; "has"; "else"; "try" ]

let private reserved = Set.ofList reservedWords

/// <summary>A bare word, unless it is a reserved one.</summary>
/// <remarks>
/// The failure is deliberately fatal: `echo eq` is a syntax error rather than a line
/// with one fewer argument, and the message says how to write the word as text. Written
/// as a primitive rather than with `failFatally` so that the position it reports is the
/// start of the word rather than the end of it — "column 7" on a seven-character line
/// points at nothing.
/// </remarks>
let private bareWordText: P<string> =
    let word = many1Chars2 wordStartChar wordChar <?> "argument"

    fun stream ->
        let start = stream.Index
        let reply = word stream

        if reply.Status = Ok && reserved.Contains reply.Result then
            stream.Seek start

            Reply(
                FatalError,
                messageError (
                    sprintf "'%s' is an operator; write \"%s\" to pass it as text" reply.Result reply.Result))
        else
            reply

/// <summary>A command's name, unless it is a reserved word.</summary>
/// <remarks>
/// Decision 0019 reserves the words in every position, and a command may not be named
/// after one. Until Phase 5 nothing enforced it in command position: `eq x` reached the
/// evaluator as an unknown command, which was harmless. `else x` is not harmless — it
/// would read as a command called `else` rather than a line with its first pipeline
/// missing — so the name is refused here, at the column it starts in.
/// </remarks>
let private commandName: P<string> =
    fun stream ->
        let start = stream.Index
        let reply = commandNameText stream

        if reply.Status = Ok && reserved.Contains reply.Result then
            stream.Seek start

            Reply(
                FatalError,
                messageError (sprintf "'%s' is a reserved word and cannot name a command" reply.Result))
        else
            reply

/// FlagIdentifier = '-'{IdentifierCharacter}+
let private flagText: P<string> =
    // A single dash only: '--flag' is not a flag, which the tests pin down. A dash in
    // front of a digit is not one either; that is a negative number, and without this
    // `echo -5` bound a flag named `5`.
    attempt (pchar '-' >>. notFollowedBy digit >>. many1Satisfy isIdentifierChar)

// ----------------------------------------------------------------- String literals
// StringCharacter = {All Printable} - ["] + {HT} + {CR} + {LF}, so a literal's body
// never contains a quote and the forms are told apart by their delimiters alone.

let private delimited (delim: string) : P<int * string> =
    attempt (
        pstring delim >>. manyChars (noneOf "\"") .>>. pstring delim
        |>> fun (body, _) -> (delim.Length, body))

/// Longest delimiter first, matching the lexer's maximal munch: `""""` is a doubled
/// empty string, not two single ones.
let private stringLiteralText: P<int * string> =
    choice [ delimited "\"\"\""      // StringLiteral4, unreachable in the .grm
             delimited "\"\""        // StringLiteral3
             delimited "\"" ]        // StringLiteral2

// The delimiter is known here, so the node is told it rather than left to count the
// quotes again from the outside, which misread a short body.
let private constant: P<Constant> =
    stringLiteralText |>> fun (quotes, body) -> StringConstant.Delimited(quotes, body) :> Constant

// ----------------------------------------------------------------- Names

// Each name is labelled for what it names. A syntax error lists what could have come
// next, and "identifier" after `$` or inside a tag says nothing a person can use; the
// page turns these labels into phrases (Phase 8), and it can only do that if the label
// says which name it was.
let private variableName: P<VariableName> =
    identifierText <?> "variable name" |>> fun n -> VariableName(Name = n)

let private objectType: P<ObjectType> = identifierText <?> "tag type" |>> fun n -> ObjectType(Value = n)

let private componentType: P<ComponentType> =
    identifierText <?> "tag type" |>> fun n -> ComponentType(Value = n)

let private propertyName: P<ProperyName> =
    identifierText <?> "property name" |>> fun n -> ProperyName(Name = n)

let private attributeName: P<TagAttributeName> =
    identifierText <?> "attribute name" |>> fun n -> TagAttributeName(Name = n)

/// The explanation for a stop with no name after it.
let memberMissingExplanation = "a column name belongs after the stop, as in $row.kind"

/// The explanation for an `@` with no name after it.
let memberAtMissingExplanation = "a name belongs after the @, as in $v.@tag"

/// <summary><MemberName> ::= '.' ( '@' )? <Identifier>, carrying its own dot so the
/// tokeniser sees `.size` as one thing.</summary>
/// <remarks>
/// Decision 0032: a stop after a variable must be followed by a name. It used to be
/// attempted, so `ls | where $row.` left the stop behind as a second argument, a path
/// called `.`, and the line failed at run time with `'where' needs a table, not text`.
/// Now the stop commits, and a stop with no name after it is a syntax error that says
/// what belongs there. Fatal, so the explanation is not lost to whatever else could
/// have followed the variable.
///
/// Decision 0048: the name may start with `@`, which marks a tag's own parts rather
/// than an attribute: `$v.@tag` is its name and `$v.@children` its children. The `@`
/// is part of the member's name, so the tree, the tokens and the evaluator all see
/// `@tag`. The `@` commits too: `$v.@` with no name after it says what belongs there.
/// Only this parser has the form; the GOLD grammar is not changed.
/// </remarks>
let private memberName: P<MemberName> =
    let own = pchar '@' >>. (identifierText <|> failFatally memberAtMissingExplanation) |>> fun name -> "@" + name

    pchar '.' >>. (((own <|> identifierText) <?> "column name") <|> failFatally memberMissingExplanation)
    |>> fun name -> MemberName(Name = name)

/// <VariableReference> ::= '$' <VariableName> ( '.' '@'? <Identifier> )*
///
/// Decision 0008: `$row.size` is how a predicate reads a column, and member access is
/// the ordinary variable notation rather than a special form inside predicates.
let private variableReference: P<VariableReference> =
    pchar '$' >>. variableName .>>. many memberName
    |>> fun (name, members) ->
            let reference = VariableReference(Name = name)
            members |> List.iter reference.Members.Add
            reference

// ----------------------------------------------------------------- Forward references
// Tags nest, and a value may be a tag, so these are tied back below.

let private instanceTag, instanceTagRef = createParserForwardedToRef<InstanceTag, unit> ()
let private tag, tagRef = createParserForwardedToRef<Tag, unit> ()
let private value, valueRef = createParserForwardedToRef<Value, unit> ()

/// <SimpleValue> ::= <Constant> | <VariableReference> | <ID>
let private simpleValue: P<SimpleValue> =
    choice [ constant |>> fun c -> c :> SimpleValue
             variableReference |>> fun v -> v :> SimpleValue
             identifierText <?> "argument" |>> fun n -> Identifier(Name = n) :> SimpleValue ]

/// A value where a bare word is allowed: the same as <SimpleValue>, with the identifier
/// widened to a word. Reads back as an Identifier so the tree and the tokeniser are
/// unchanged; the evaluator already treats an identifier as "text the command decides".
let private argumentSimpleValue: P<SimpleValue> =
    choice [ constant |>> fun c -> c :> SimpleValue
             variableReference |>> fun v -> v :> SimpleValue
             bareWordText |>> fun n -> Identifier(Name = n) :> SimpleValue ]

// ----------------------------------------------------------------- Tag attributes
// <TagAttribute> ::= <TagAttributeName> '=' <ArgumentSimpleValue>
//
// Decision 0007: the value is a bare word rather than a plain identifier, so a tag can
// carry a path without quotes. The word parser stops before `/>`, so the closing
// bracket is still the tag's and not the path's.

let private tagAttribute: P<TagAttribute> =
    // The name and '=' are attempted together so a bare identifier that is not an
    // attribute leaves the input untouched for whatever follows.
    attempt (attributeName .>> ws .>> pchar '=' .>> ws) .>>. (argumentSimpleValue .>> ws)
    |>> fun (name, v) -> TagAttribute(Name = name, Value = v)

let private tagAttributeList: P<TagAttributeList> =
    many tagAttribute
    |>> fun attributes ->
            let list = TagAttributeList()
            attributes |> List.iter list.Attributes.Add
            list

// ----------------------------------------------------------------- Object instances

/// Decision 0007: `<` is a tag opener only when the very next character could start a
/// tag — a name, a `$` or the `/` of a closing tag. With a space after it, or anything
/// else, it is not a tag, which is what leaves `<` free to become less-than in Phase 3.
/// The lookahead also has to be the reason the `ws` after `<` is gone: allowing space
/// there is precisely what would make `a < b` a tag.
let private tagOpen: P<unit> =
    attempt (pchar '<' >>. followedBy (satisfy (fun c -> isIdentifierChar c || c = '$' || c = '/')))

/// <summary>The part shared by the closed and open forms: '&lt;' [name '|'] type attributes</summary>
/// <remarks>
/// Only the opening and the type are attempted: until the type has been read, `<$x>`
/// or a closing tag may still be what this is. Once it has, it is a tag, and a failure
/// in its attributes is that tag's failure. Attempting the whole header turned the
/// fatal error for `<t a=eq/>` into a backtrack to column 0 with the explanation lost.
/// </remarks>
let private objectHeader: P<VariableName option * ObjectType * TagAttributeList> =
    attempt (
        tagOpen
        >>. (opt (attempt (variableName .>> ws .>> pchar '|' .>> ws)))
        .>>. (objectType .>> ws))
    .>>. tagAttributeList
    |>> fun ((name, typ), attributes) -> (name, typ, attributes)

/// <ClosingObjectTag> ::= '<' '/' <ObjectType> '>' | '<' '/' '>'
let private closingObjectTag: P<ObjectType option> =
    symStr "</"
    >>. ((pchar '>' >>% None) <|> (objectType .>> ws .>> pchar '>' |>> Some))

let private objectInstance: P<ObjectInstance> =
    objectHeader
    >>= fun (name, typ, attributes) ->
        // Closed form ends at '/>'; anything else is an opening tag with a body.
        (attempt (symStr "/>")
         >>% ObjectInstance(VariableName = Option.toObj name, ObjectType = typ, Attributes = attributes, Children = null))
        <|> (sym '>' >>. many tag .>>. closingObjectTag
             |>> fun (children, closing) ->
                    match closing with
                    | Some closingType when closingType.Value <> typ.Value ->
                        // The .grm allows any name here; a mismatch is a mistake, and
                        // the old interpreter only recorded it as a message.
                        failwithf "Closing tag '%s' does not match opening tag '%s'" closingType.Value typ.Value
                    | _ ->
                        let list = TagList()
                        children |> List.iter list.Tags.Add
                        ObjectInstance(
                            VariableName = Option.toObj name,
                            ObjectType = typ,
                            Attributes = attributes,
                            Children = (if list.Tags.Count = 0 then null else list)))

// ----------------------------------------------------------------- Component instances
// The brace equivalent of an object tag. Every production for these threw before.

let private componentHeader: P<VariableName option * ComponentType * TagAttributeList> =
    pchar '{' >>. ws
    >>. pipe3
            (opt (attempt (variableName .>> ws .>> pchar '|' .>> ws)))
            (componentType .>> ws)
            tagAttributeList
            (fun name typ attributes -> (name, typ, attributes))

let private closingComponentTag: P<ComponentType option> =
    symStr "{/"
    >>. ((pchar '}' >>% None) <|> (componentType .>> ws .>> pchar '}' |>> Some))

let private componentInstance: P<ComponentInstance> =
    attempt componentHeader
    >>= fun (name, typ, attributes) ->
        (attempt (symStr "/}")
         >>% ComponentInstance(VariableName = Option.toObj name, ComponentType = typ, Attributes = attributes, Children = null))
        <|> (sym '}' >>. many tag .>>. closingComponentTag
             |>> fun (children, closing) ->
                    match closing with
                    | Some closingType when closingType.Value <> typ.Value ->
                        failwithf "Closing tag '%s' does not match opening tag '%s'" closingType.Value typ.Value
                    | _ ->
                        let list = TagList()
                        children |> List.iter list.Tags.Add
                        ComponentInstance(
                            VariableName = Option.toObj name,
                            ComponentType = typ,
                            Attributes = attributes,
                            Children = (if list.Tags.Count = 0 then null else list)))

/// <VariableTag> ::= '<' '$' <VariableName> '>'
let private variableTag: P<VariableTag> =
    attempt (symStr "<$") >>. variableName .>> ws .>> pchar '>'
    |>> fun n -> VariableTag(Name = n)

// ----------------------------------------------------------------- Property assignment
// All four forms, where only '[name=value]' used to build.

let private propertyAssignment: P<PropertyAssignment> =
    attempt (pchar '[' >>. ws >>. propertyName .>> ws)
    >>= fun name ->
        choice
            [ // [name=value]
              attempt (pchar '=' >>. ws >>. simpleValue .>> ws .>> pchar ']')
              |>> fun v -> PropertyAssignment(Name = name, Value = v)

              // [name]=<tag>
              attempt (pchar ']' >>. ws >>. pchar '=' >>. ws >>. instanceTag)
              |>> fun t ->
                    let list = TagList()
                    list.Tags.Add t
                    PropertyAssignment(Name = name, Children = list)

              // [name] tags [/name] or [name] tags [/]
              pchar ']' >>. ws >>. many tag
              .>> symStr "[/"
              .>> (attempt (pchar ']' >>% ()) <|> (propertyName .>> ws .>> pchar ']' >>% ()))
              |>> fun tags ->
                    let list = TagList()
                    tags |> List.iter list.Tags.Add
                    PropertyAssignment(Name = name, Children = list) ]

// ----------------------------------------------------------------- Tags and values

instanceTagRef.Value <-
    choice [ variableTag |>> fun t -> t :> InstanceTag
             objectInstance |>> fun t -> t :> InstanceTag
             componentInstance |>> fun t -> t :> InstanceTag ]
    .>> ws

tagRef.Value <-
    choice [ attempt (propertyAssignment |>> fun t -> t :> Tag)
             attempt (variableTag |>> fun t -> t :> Tag)
             attempt (objectInstance |>> fun t -> t :> Tag)
             attempt (componentInstance |>> fun t -> t :> Tag) ]
    .>> ws

/// <Value> ::= <SimpleValue> | <InstanceTag>
///
/// A tag is carried across into the value hierarchy by TagValue, because InstanceTag
/// already descends from Tag and a record cannot descend from Value as well.
valueRef.Value <-
    choice [ instanceTag |>> fun t -> TagValue(Tag = t) :> Value
             simpleValue |>> fun v -> v :> Value ]

/// <Value> in a command-argument position, where bare words are allowed.
let private argumentValue: P<Value> =
    choice [ instanceTag |>> fun t -> TagValue(Tag = t) :> Value
             argumentSimpleValue |>> fun v -> v :> Value ]

// ----------------------------------------------------------------- Expressions
//
// Phase 3. Decision 0007 chose words over symbols, so there is no lexical clash with
// tags to resolve here: an operator is a whole word, and decision 0019 keeps those
// words out of every other position so `eq` can never be an argument by accident.
//
//   Expression ::= OrExpr
//   OrExpr     ::= AndExpr ( "or" AndExpr )*
//   AndExpr    ::= NotExpr ( "and" NotExpr )*
//   NotExpr    ::= "not" NotExpr | Comparison
//   Comparison ::= Operand ( CompareOp Operand )?
//   Operand    ::= ArgumentValue | "(" Pipeline ")"
//
// A comparison with no operator is the operand itself rather than a wrapper around it,
// which is what keeps every line written before expressions existed parsing to exactly
// the tree it used to.

let private pipeline, pipelineRef = createParserForwardedToRef<PipedCommandList, unit> ()

/// A word operator: the whole word and nothing longer, so `eq` is an operator and
/// `equals` is an argument.
let private operatorWord (word: string) : P<OperatorWord> =
    attempt (pstring word .>> notFollowedBy (satisfy isWordChar))
    .>> ws
    |>> fun name -> OperatorWord(Name = name)

let private comparisonOperator: P<OperatorWord> =
    choice (
        [ "eq"; "ne"; "ge"; "gt"; "le"; "lt"; "like"; "has" ]
        |> List.map operatorWord)

/// <Operand> ::= <ArgumentValue> | '(' <PipedCommandList> ')'
let private operand: P<Value> =
    choice
        [ attempt (pchar '(' >>. ws) >>. pipeline .>> pchar ')'
          |>> fun nested -> NestedPipeline(Pipeline = nested) :> Value

          argumentValue ]
    .>> ws

/// <summary>An example of what a comparison operator can be given.</summary>
/// <remarks>
/// Only for the explanation below, so a number where the operator orders and a glob
/// where it matches one, and a word everywhere else.
/// </remarks>
let private exampleFor (op: string) =
    match op with
    | "gt" | "ge" | "lt" | "le" -> "10"
    | "like" -> "\"*.txt\""
    | _ -> "folder"

/// <summary>The right side of a comparison, which has to be there.</summary>
/// <remarks>
/// Phase 8: `ls | where $row.kind eq` used to fail with a list of every symbol that
/// can start a value. What was missing is one thing, and the operator knows what it
/// is, so the error says it: `eq needs a value to compare with, such as folder`. Only
/// when nothing that could be an operand was started: an operand that began and went
/// wrong, such as an unclosed quote or a reserved word, keeps its own error.
/// </remarks>
let private rightOperand (op: OperatorWord) : P<Value> =
    fun stream ->
        let before = stream.StateTag
        let reply = operand stream

        // A quote that never closes also fails without moving, and that is the
        // string's error, not a missing value.
        if reply.Status = Error && stream.StateTag = before && stream.Peek() <> '"' then
            Reply(
                FatalError,
                messageError (sprintf "%s needs a value to compare with, such as %s" op.Name (exampleFor op.Name)))
        else
            reply

let private comparison: P<Value> =
    operand .>>. opt (comparisonOperator >>= fun op -> rightOperand op |>> fun right -> (op, right))
    |>> function
        | left, None -> left
        | left, Some(op, right) ->
            ComparisonExpression(Left = left, Operator = op, Right = right) :> Value

let private notExpression, notExpressionRef = createParserForwardedToRef<Value, unit> ()

notExpressionRef.Value <-
    (operatorWord "not" .>>. notExpression
     |>> fun (op, operand) -> NotExpression(Operator = op, Operand = operand) :> Value)
    <|> comparison

/// Left associative, so `a and b and c` reads as `(a and b) and c` and means the same.
let private foldBinary (first: Value, rest: (OperatorWord * Value) list) =
    rest
    |> List.fold
        (fun left (op, right) -> BooleanExpression(Left = left, Operator = op, Right = right) :> Value)
        first

let private andExpression: P<Value> =
    notExpression .>>. many (operatorWord "and" .>>. notExpression) |>> foldBinary

let private orExpression: P<Value> =
    andExpression .>>. many (operatorWord "or" .>>. andExpression) |>> foldBinary

/// <Expression> ::= <OrExpr>
let private argumentExpression: P<Value> = orExpression

// ----------------------------------------------------------------- Commands

/// <FunctionArgument> ::= <RequiredArgument> | <OptionalArgument>
let private functionArgument: P<CommandArgument> =
    choice
        [ // <OptionalArgument> ::= <ID> ':' <Value>
          // Labelled as an argument: it is one, and "identifier" would read as a
          // command name.
          attempt ((identifierText <?> "argument") .>> ws .>> pchar ':' .>> ws) .>>. argumentExpression
          |>> fun (name, v) ->
                OptionalCommandArgument(Name = OneOf.OneOf<CommandArgumentFlag, Identifier>.op_Implicit (Identifier(Name = name)), Value = v)
                :> CommandArgument

          // <RequiredArgument> ::= <Expression>
          argumentExpression |>> fun v -> RequiredCommandArgument(Value = v) :> CommandArgument ]
    .>> ws

/// <FunctionArgumentList> ::= <FunctionArgumentList> ',' <FunctionArgument>
///
/// The comma production was the one GOLD never implemented, so every function call was
/// limited to a single argument.
let private functionArgumentList: P<CommandArguments> =
    sepBy functionArgument (sym ',')
    |>> fun arguments ->
            let list = CommandArguments()
            arguments |> List.iter list.Arguments.Add
            list

/// <summary>
/// <FunctionExpression> ::= <CommandName> '(' <FunctionArgumentList> ')', with the
/// parenthesis against the name.
/// </summary>
/// <remarks>
/// Decision 0023: `first(ls)` calls `first`, and `first (ls)` is `first` handed the
/// result of the pipeline in parentheses. Allowing space before the parenthesis made
/// the second one unwritable, and made `first (ls | count)` a syntax error, because the
/// function form committed at the `(` and a pipe is not a function argument.
/// </remarks>
let private functionExpression: P<FunctionExpression> =
    attempt (commandName .>> pchar '(' .>> ws)
    .>>. (functionArgumentList .>> ws .>> pchar ')' .>> ws)
    |>> fun (name, arguments) -> FunctionExpression(Id = Identifier(Name = name), Arguments = arguments)

/// <Assignment> ::= <Identifier> '=' <ArgumentValue>, with no spaces around the '='
///
/// Decision 0017: this is data rather than parameter binding, which is what lets
/// `attr notes.txt tag=work` carry an attribute name the command has never heard of.
/// The no-space rule is what keeps `echo a = b` three ordinary words, so neither side
/// may be padded and `ws` is deliberately absent between the three parts.
let private assignmentArgument: P<CommandArgument> =
    // Labelled as an argument, which is what it is to the person writing it.
    attempt ((identifierText <?> "argument") .>> pchar '=') .>>. argumentValue
    |>> fun (name, v) ->
            AssignmentArgument(Name = Identifier(Name = name), Value = v) :> CommandArgument

/// <CommandArgument> ::= <Flag> | <Assignment> | <Expression>
let private commandArgument: P<CommandArgument> =
    choice [ flagText |>> fun f -> CommandArgumentFlag(Name = f) :> CommandArgument
             // Before the plain value, because a value would otherwise swallow the name
             // as a word and leave `=work` behind.
             assignmentArgument
             argumentExpression |>> fun v -> CommandArgumentValue(Value = v) :> CommandArgument ]
    .>> ws

/// <summary>`else` ends an argument list rather than being refused by it.</summary>
/// <remarks>
/// A reserved word in argument position is a fatal error (decision 0019), which is
/// right for `echo eq` and wrong for `read x else echo none`: there the word is not an
/// argument at all, it is where the pipeline stops. So the list looks for it first and
/// ends there, and the line grammar takes it from there.
/// </remarks>
let private elseKeyword: P<unit> =
    attempt (skipString "else" .>> notFollowedBy (satisfy isWordChar)) .>> ws

let private commandArgumentList: P<CommandArguments> =
    many (notFollowedBy elseKeyword >>. commandArgument)
    |>> fun arguments ->
            let list = CommandArguments()
            arguments |> List.iter list.Arguments.Add
            list

/// <CommandExpression_CLINotation> ::= <ID> <CommandArgumentList>
let private cliExpression: P<CommandExpressionCli> =
    commandName .>> ws .>>. commandArgumentList
    |>> fun (name, arguments) -> CommandExpressionCli(Name = CommandName(Name = name), Arguments = arguments)

type private StageForm =
    OneOf.OneOf<FunctionExpression, CommandExpressionCli, InstanceTag, NestedPipeline, VariableReference>

/// <summary>
/// <CommandExpression> ::= <FunctionExpression> | <CommandExpression_CLINotation>
///                       | <IndividualCLIValue> | '(' <PipedCommandList> ')'
///                       | <VariableReference>
/// </summary>
/// <remarks>
/// The fourth alternative is Phase 5's: a pipeline in parentheses can stand as a stage,
/// so `try (read notes.txt) | set r` marks exactly the part that may fail.
///
/// The last is Phase 8's (decision 0032): a variable, with or without members, can
/// stand as a stage the way a tag already could, so `$files | count`, `$problem.kind`
/// and `$maybe ?? "x"` are lines. `$` cannot start a command name, so the alternative
/// takes nothing away from the others.
/// </remarks>
let private commandExpression: P<StageForm> =
    choice
        [ functionExpression |>> StageForm.op_Implicit
          cliExpression |>> StageForm.op_Implicit
          // <IndividualCLIValue> ::= <InstanceTag>, which includes a variable tag. The
          // old interpreter tested for ObjectInstance specifically, so `<$name>` fell
          // through every branch and threw.
          instanceTag |>> StageForm.op_Implicit
          attempt (pchar '(' >>. ws) >>. pipeline .>> pchar ')'
          |>> fun nested -> StageForm.op_Implicit(NestedPipeline(Pipeline = nested))
          variableReference |>> StageForm.op_Implicit ]
    .>> ws

/// The `try` in front of a stage. A whole word, so `trying` is still a command name
/// the list does not have rather than `try` and `ing`.
let private tryKeyword: P<unit> =
    attempt (skipString "try" .>> notFollowedBy (satisfy isWordChar)) .>> ws

/// <summary><Stage> ::= "try"? <CommandExpression> ( "??" <Operand> )?</summary>
/// <remarks>
/// Decision 0014. Both markers belong to one stage, not to the pipeline: `try read x |
/// set problem` turns `read`'s failure into a value that `set` receives, and `first (ls)
/// ?? "none"` defaults what `first` answered. `?` is not a word character, so an
/// argument list always stops in front of `??` without being told to.
/// </remarks>
let private stage: P<CommandExpression> =
    pipe3
        (opt tryKeyword)
        commandExpression
        (opt (attempt (skipString "??") >>. ws >>. operand))
        (fun marker form fallback ->
            CommandExpression(
                Expression = form,
                Try = marker.IsSome,
                Default =
                    (match fallback with
                     | Some operand -> operand
                     | None -> null)))

/// <PipedCommandList> ::= <PipedCommandList> '|' <Stage> | <Stage>
let private pipedCommandList: P<PipedCommandList> =
    sepBy1 stage (sym '|')
    |>> fun commands ->
            let list = PipedCommandList()
            commands |> List.iter list.OrderedCommands.Add
            list

pipelineRef.Value <- pipedCommandList

/// <summary><Line> ::= <PipedCommandList> ( "else" <PipedCommandList> )*</summary>
/// <remarks>
/// Decision 0014: `else` binds looser than `|`, so `a | b else c | d` is `(a | b) else
/// (c | d)`. A line with no `else` is the pipeline itself rather than a line of one, so
/// every tree written before Phase 5 is exactly what it was.
/// </remarks>
let private line: P<RootNode> =
    sepBy1 pipedCommandList elseKeyword
    |>> function
        | [ single ] -> single :> RootNode
        | pipelines ->
            let recovery = RecoveryLine()
            pipelines |> List.iter recovery.Pipelines.Add
            recovery :> RootNode

/// <Program> ::= <Line> | ! Empty
///
/// The empty alternative is decided by looking for end of input rather than by trying
/// the command list and backtracking. Wrapping the command list in `attempt` would undo
/// its position on any failure, and every syntax error would then be reported at
/// column 0 instead of where the input actually went wrong.
let program: P<RootNode> =
    ws
    >>. ((eof >>% (EmptyCommand() :> RootNode))
         <|> (line .>> ws .>> eof))

/// <summary>An expression on its own, with nothing around it.</summary>
/// <remarks>
/// Phase 4: a saved view is a file whose content is the predicate text, so reading one
/// back means parsing a predicate that was never part of a command line. The same
/// grammar rule serves both, so a view saved from `in $row.kind eq note` reads back as
/// exactly the expression that was written.
/// </remarks>
let expressionOnly: P<Value> = ws >>. argumentExpression .>> ws .>> eof

/// The identifier grammar, used to validate a name on its own.
let identifierOnly: P<Identifier> =
    ws >>. identifierText .>> ws .>> eof |>> fun n -> Identifier(Name = n)
