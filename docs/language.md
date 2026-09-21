# The command language

Everything you type is parsed into a tree before anything runs. This page covers every
form the parser accepts, what each one means, and how the words you write reach a
command's parameters.

Examples show what the terminal displays underneath. Results that appear as tappable
chips are shown as plain words here.

## One line, one pipeline

A command line is a single line. Spaces and tabs separate words; a newline ends the
program, so there is no line continuation and no multi-command script. There are no
comments.

The grammar is case insensitive when matching command names, and the text you typed is
preserved exactly in the tree.

## Three ways to call a command

The same command can be written three ways. They produce the same call.

### Command line form

The name, then arguments separated by spaces.

```
$ cat readme.txt
This filesystem lives in the browser tab.
```

### Function form

The name, then arguments in parentheses separated by commas. Use `name: value` to pick
a parameter by name.

```
$ write(note.txt, hello)
note.txt
$ cat(path: readme.txt)
This filesystem lives in the browser tab.
```

Function form is the only form that takes `name: value`. In command line form, name an
argument with a flag instead: `ls -path documents`.

### Tag form

An instance tag on its own is an expression that produces a value without calling a
command. See [Tags](#tags-objects-and-components).

```
$ <measurement unit=metres value=3/>
<measurement unit=metres value=3/>
```

## Arguments

### Bare words

An unquoted word may contain letters, digits and underscore, plus `. \ / : ~ + @ % -`
after the first character. The first character must be a letter, digit, underscore, or
one of `. \ / ~`. That covers the things a shell needs to write without ceremony:

```
$ echo notes.txt
notes.txt
$ echo ../documents
../documents
$ echo C:\Users\me
C:\Users\me
$ echo well-known
well-known
$ echo user@host
user@host
```

Accented letters are ordinary identifier characters, so `café` and `größe` need no
quotes.

Bare words are only recognised where a command argument is expected. Inside a tag, `/`
closes the tag, so a tag attribute takes an identifier, a string or a variable and not
a path.

### Numbers

A word that reads as a number becomes a number, not text. The rest stay text.

```
$ echo 42
42
$ echo 3.5
3.5
$ echo v2
v2
```

Numbers use a point as the decimal separator regardless of your locale. Commands that
want a number, such as `progress`, accept either a number or a word that looks like one.

### Quoted strings

Use quotes for anything containing a space. A string's body cannot contain a double
quote, so there is no escape character. Three delimiters are accepted, and they exist so
a string can be quoted more heavily without changing its meaning:

| Written | Value |
| --- | --- |
| `"hello world"` | `hello world` |
| `""hello world""` | `hello world` |
| `"""hello world"""` | `hello world` |

```
$ echo "hello world"
hello world
$ echo ""quoted twice""
quoted twice
```

The number of quotes is kept in the tree, so re-serialising your command gives back
exactly what you typed.

### Flags

A flag is a single dash followed by a name: `-path`. Two dashes is a syntax error.

A flag takes the next plain value as its value:

```
$ ls -path documents
up  notes.txt
```

A flag with nothing after it is a switch and binds `true`:

```
$ ls -path
Directory does not exist : /home/terminal/true
```

That message is what a switch looks like when the command wanted a value. Flags bind
`true` only because some parameters are genuinely switches.

### Named arguments in function form

```
$ echo(text: hello)
hello
```

The name must match a declared parameter name or its flag. `echo(nonsense: 1)` reports
`'echo' has no argument named 'nonsense'.`

### Tags as arguments

A tag may be written wherever an argument is expected, and the command receives the
object or component it builds:

```
$ set shape <square side=2/>
<square side=2/>
$ echo {renderer colour=red/}
{renderer colour=red/}
```

A tag that names a variable still binds it, even when it is an argument.

### Variables as arguments

`$name` is replaced by the value bound to that name, whatever its type:

```
$ set n 42
42
$ echo $n
42
```

## How arguments reach parameters

Each command declares parameters in order. Binding happens in one pass:

1. Named arguments and flags bind to the parameter they name, wherever they appear.
2. Remaining arguments fill the remaining parameters in declaration order, optional
   parameters included.
3. A parameter still unfilled takes the piped value, if the parameter accepts one and
   something was piped in.
4. A parameter still unfilled takes its default, if it is optional.
5. Otherwise the command reports `'name' needs an argument for 'parameter'.`
6. Arguments left over after every parameter is filled report
   `'name' takes N arguments, but M were given.`

So `write` takes a path and a text, and these are all the same call:

```
write note.txt hello
write(note.txt, hello)
echo hello | write note.txt
```

## Pipes

`|` runs the commands left to right and hands each command's result to the next one as
its input. The value keeps its type: a list stays a list, a path stays a path.

```
$ ls | set files
up  documents  projects  readme.txt
$ echo documents | cd
documents
$ cat notes.txt | write copy.txt
copy.txt
```

A command uses the piped value only for a parameter that accepts one and that you did
not write out yourself. `cat` and `cd` take their path that way; `write` takes its text
that way, because its path is the argument you are most likely to write.

If a stage fails, the pipeline stops there and reports that stage's error.

```
$ echo nowhere | cd
Directory does not exist : nowhere
```

## Variables

Bind a value with `set`, or with a tag (below). Read it with `$name`.

```
$ set greeting hello
hello
$ set files
'set' needs an argument for 'value'.
$ ls | set entries
up  documents  projects  readme.txt
$ vars
$entries = up documents\ projects\ readme.txt
$greeting = hello
```

Names may contain letters, digits and underscore. Writing `set $x 1` does not name a
variable `x`; `$x` is read as a variable reference, so it reports
`Unknown variable : $x`.

Undoing a `set` restores whatever the name was bound to before, or unbinds it if it was
new.

## Tags: objects and components

Tags build structured values directly, without a command. Angle brackets make an
object; braces make a component. The distinction mirrors an entity component system:
entities are things, components are what is attached to them.

### Objects

```
$ <measurement unit=metres value=3/>
<measurement unit=metres value=3/>
$ <outer><inner depth=2/></outer>
<outer><inner depth=2/></outer>
```

An attribute's value is a string, a variable reference or an identifier. A closing tag
may repeat the type or be empty: `</thing>` and `</>` are both accepted. A closing tag
that names a different type is an error:

```
$ <a></b>
Closing tag 'b' does not match opening tag 'a'
```

### Components

```
$ {renderer colour=red/}
{renderer colour=red/}
```

Components read back the way you wrote them, with braces, so an object and a component
of the same name stay distinguishable.

### Binding a tag to a variable

Put a name and a `|` before the type:

```
$ <size|measurement unit=metres/>
<measurement unit=metres/>
$ echo $size
<measurement unit=metres/>
$ set copy <$size>
<measurement unit=metres/>
```

The variable name is not part of the value, which is why the result above reads
`<measurement unit=metres/>`.

### Reading a variable back as a tag

```
$ <$size>
<measurement unit=metres/>
```

### Property assignment

The grammar also accepts property tags inside a tag body, in four forms:
`[name=value]`, `[name]=<tag>`, `[name] tags [/name]` and `[name] tags [/]`. They parse
into the tree, but nothing evaluates them yet:

```
$ <t>[size=3]</t>
Cannot evaluate a child PropertyAssignment.
```

## What the language does not have

These are absent by design or not built yet. Nothing here silently half-works.

| Not supported | What to do instead |
| --- | --- |
| Wildcards such as `*.txt` | Name files individually. |
| Redirection `>` and `>>` | Pipe into `write`. |
| Several commands per line with `;` or `&&` | Run them one at a time. |
| Escapes inside strings | Strings cannot contain a double quote at all. |
| Background jobs | One command runs at a time; use Stop to end it. |
| Environment variables | Use `set` and `$name`. |

## Reading the tree back

The terminal colours each word by the role the grammar gave it, and tapping a word in
the scrollback names that role. The roles are: command, flag, string, variable,
identifier, type, attribute and punctuation.

Because the tree is faithful, serialising it reproduces your text exactly. That
round-trip is checked on every parse, which is what keeps colouring and structure
honest.
