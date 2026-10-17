// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
internal static class SettingsPreparation
{
    public static SyntaxTree[] Apply(IEnumerable<SyntaxTree> input,string name)
    {
        var trees=input.ToArray();
        var classes=trees.SelectMany(tree=>tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()).Where(node=>node.Identifier.ValueText==name).ToArray();
        if(classes.Length==0)return trees;
        var fields=classes.SelectMany(node=>node.Members.OfType<FieldDeclarationSyntax>())
            .SelectMany(field=>field.Declaration.Variables.Select(variable=>(Name:variable.Identifier.ValueText,Type:field.Declaration.Type))).ToDictionary(pair=>pair.Name,pair=>pair.Type);
        var factories=new Dictionary<string,string>();
        foreach(var init in classes.SelectMany(node=>node.Members.OfType<MethodDeclarationSyntax>()).Where(node=>node.Identifier.ValueText=="Init"))
        foreach(var assignment in init.DescendantNodes().OfType<AssignmentExpressionSyntax>()){
            if(assignment.Left is not IdentifierNameSyntax field||!fields.TryGetValue(field.Identifier.ValueText,out var type))continue;
            if(assignment.Right is not BinaryExpressionSyntax coalesce||!coalesce.IsKind(SyntaxKind.CoalesceExpression))continue;
            if(coalesce.Left is not InvocationExpressionSyntax load||!load.ToString().Contains("Load"))continue;
            var value=coalesce.Right;
            if(value.DescendantNodesAndSelf().Any(node=>node is InvocationExpressionSyntax or AwaitExpressionSyntax or AnonymousFunctionExpressionSyntax))continue;
            string typeName=type.ToString().TrimEnd('?');
            if(value is ImplicitObjectCreationExpressionSyntax implicitNew)value=SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(typeName)).WithArgumentList(implicitNew.ArgumentList).WithInitializer(implicitNew.Initializer);
            if(value is not ObjectCreationExpressionSyntax)continue;
            factories.TryAdd(typeName,value.NormalizeWhitespace().ToFullString());
        }
        if(factories.Count==0)return trees;
        string checks=string.Join("\n",factories.Select(pair=>$"if(type==typeof({pair.Key}))return {pair.Value};"));
        var method=SyntaxFactory.ParseMemberDeclaration("protected override object CreateSettingsConfiguration(System.Type type){"+checks+"return base.CreateSettingsConfiguration(type);}")!;
        var target=classes[0];
        return trees.Select(tree=>tree==target.SyntaxTree?tree.WithRootAndOptions(tree.GetRoot().ReplaceNode(target,target.AddMembers(method)),tree.Options):tree).ToArray();
    }
    public static Dictionary<string,string> Labels(IEnumerable<SyntaxTree> trees)
    {
        var labels=new Dictionary<string,string>();
        foreach(var call in trees.SelectMany(tree=>tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())){
            var member=call.ArgumentList.Arguments.FirstOrDefault(argument=>argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)&&argument.Expression is MemberAccessExpressionSyntax access&&access.Expression is IdentifierNameSyntax receiver&&(receiver.Identifier.ValueText is "config" or "Config"));
            if(member?.Expression is not MemberAccessExpressionSyntax field||call.ArgumentList.Arguments.Count==0)continue;
            var expression=call.ArgumentList.Arguments[0].Expression;
            var language=expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault(node=>node.Expression.ToString()=="Lang.Get");
            var literal=language?.ArgumentList.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax??expression as LiteralExpressionSyntax;
            if(literal?.IsKind(SyntaxKind.StringLiteralExpression)==true)labels.TryAdd(field.Name.Identifier.ValueText,literal.Token.ValueText);
        }
        return labels;
    }
}

