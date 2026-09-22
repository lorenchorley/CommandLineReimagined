/// The parsed tree, under names that cannot be mistaken for the runtime ones.
///
/// `Commands.Parser.SemanticTree.Value` and `CommandLineReimagined.Core.Value` are both
/// called `Value` and mean opposite halves of the same idea: one is what was written,
/// the other is what running it produced. Opening the parser's namespace would shadow
/// the runtime type silently, so the core never opens it and says `Tree.Value` instead.
module CommandLineReimagined.Core.Tree

type Node = Commands.Parser.SemanticTree.RootNode
type Empty = Commands.Parser.SemanticTree.EmptyCommand
type Pipeline = Commands.Parser.SemanticTree.PipedCommandList
type Expression = Commands.Parser.SemanticTree.CommandExpression
type Cli = Commands.Parser.SemanticTree.CommandExpressionCli
type Function = Commands.Parser.SemanticTree.FunctionExpression

type Value = Commands.Parser.SemanticTree.Value
type SimpleValue = Commands.Parser.SemanticTree.SimpleValue
type Constant = Commands.Parser.SemanticTree.Constant
type StringConstant = Commands.Parser.SemanticTree.StringConstant
type Identifier = Commands.Parser.SemanticTree.Identifier
type VariableReference = Commands.Parser.SemanticTree.VariableReference
type VariableName = Commands.Parser.SemanticTree.VariableName
type MemberName = Commands.Parser.SemanticTree.MemberName
type TagValue = Commands.Parser.SemanticTree.TagValue

type ExpressionNode = Commands.Parser.SemanticTree.ExpressionNode
type ComparisonExpression = Commands.Parser.SemanticTree.ComparisonExpression
type BooleanExpression = Commands.Parser.SemanticTree.BooleanExpression
type NotExpression = Commands.Parser.SemanticTree.NotExpression
type NestedPipeline = Commands.Parser.SemanticTree.NestedPipeline
type OperatorWord = Commands.Parser.SemanticTree.OperatorWord

type Tag = Commands.Parser.SemanticTree.Tag
type TagList = Commands.Parser.SemanticTree.TagList
type InstanceTag = Commands.Parser.SemanticTree.InstanceTag
type ObjectInstance = Commands.Parser.SemanticTree.ObjectInstance
type ComponentInstance = Commands.Parser.SemanticTree.ComponentInstance
type VariableTag = Commands.Parser.SemanticTree.VariableTag
type TagAttribute = Commands.Parser.SemanticTree.TagAttribute
type TagAttributeList = Commands.Parser.SemanticTree.TagAttributeList

type Arguments = Commands.Parser.SemanticTree.CommandArguments
type Argument = Commands.Parser.SemanticTree.CommandArgument
type RequiredArgument = Commands.Parser.SemanticTree.RequiredCommandArgument
type ArgumentValue = Commands.Parser.SemanticTree.CommandArgumentValue
type NamedArgument = Commands.Parser.SemanticTree.OptionalCommandArgument
type Flag = Commands.Parser.SemanticTree.CommandArgumentFlag
type Assignment = Commands.Parser.SemanticTree.AssignmentArgument
