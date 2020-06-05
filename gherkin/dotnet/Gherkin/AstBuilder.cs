using System;
using System.Collections.Generic;
using System.Linq;
using Io.Cucumber.Messages;

namespace Gherkin
{
    public class AstBuilder<T> : IAstBuilder<T>
    {
        private readonly Stack<AstNode> stack = new Stack<AstNode>();
        private AstNode CurrentNode { get { return stack.Peek(); } }
        private List<GherkinDocument.Types.Comment> comments = new List<GherkinDocument.Types.Comment>();

        public AstBuilder()
        {
            Reset();
        }

        public void Reset()
        {
            stack.Clear();
            stack.Push(new AstNode(RuleType.None));
            comments.Clear();
        }

        public void Build(Token token)
        {
            if (token.MatchedType == TokenType.Comment)
            {
                comments.Add(CreateComment(GetLocation(token), token.MatchedText));
            }
            else
            {
                CurrentNode.Add((RuleType)token.MatchedType, token);
            }
        }

        public void StartRule(RuleType ruleType)
        {
            stack.Push(new AstNode(ruleType));
        }

        public void EndRule(RuleType ruleType)
        {
            var node = stack.Pop();
            object transformedNode = GetTransformedNode(node);
            CurrentNode.Add(node.RuleType, transformedNode);
        }

        public T GetResult()
        {
            return CurrentNode.GetSingle<T>(RuleType.GherkinDocument);
        }

        private object GetTransformedNode(AstNode node)
        {
            switch (node.RuleType)
            {
                case RuleType.Step:
                    {
                        var stepLine = node.GetToken(TokenType.StepLine);
                        var dataTable = node.GetSingle<GherkinDocument.Types.Feature.Types.Step.Types.DataTable>(RuleType.DataTable);
                        var docString = node.GetSingle<GherkinDocument.Types.Feature.Types.Step.Types.DocString>(RuleType.DocString);
                        return CreateStep(GetLocation(stepLine), stepLine.MatchedKeyword, stepLine.MatchedText, docString, dataTable, node);
                    }
                case RuleType.DocString:
                    {
                        var separatorToken = node.GetTokens(TokenType.DocStringSeparator).First();
                        var contentType = separatorToken.MatchedText.Length == 0 ? String.Empty : separatorToken.MatchedText;
                        var lineTokens = node.GetTokens(TokenType.Other);
                        var content = string.Join(Environment.NewLine, lineTokens.Select(lt => lt.MatchedText));

                        return CreateDocString(GetLocation(separatorToken), contentType, content, node);
                    }
                case RuleType.DataTable:
                    {
                        var rows = GetTableRows(node);
                        return CreateDataTable(rows, node);
                    }
                case RuleType.Background:
                    {
                        var backgroundLine = node.GetToken(TokenType.BackgroundLine);
                        var description = GetDescription(node);
                        var steps = GetSteps(node);
                        return CreateBackground(GetLocation(backgroundLine), backgroundLine.MatchedKeyword, backgroundLine.MatchedText, description, steps, node);
                    }
                case RuleType.ScenarioDefinition:
                    {
                        var tags = GetTags(node);

                        var scenarioNode = node.GetSingle<AstNode>(RuleType.Scenario);
                        var scenarioLine = scenarioNode.GetToken(TokenType.ScenarioLine);

                        var description = GetDescription(scenarioNode);
                        var steps = GetSteps(scenarioNode);
                        var examples = scenarioNode.GetItems<GherkinDocument.Types.Feature.Types.Scenario.Types.Examples>(RuleType.ExamplesDefinition).ToArray();
                        return CreateScenario(tags, GetLocation(scenarioLine), scenarioLine.MatchedKeyword, scenarioLine.MatchedText, description, steps, examples, node);
                    }
                case RuleType.ExamplesDefinition:
                    {
                        var tags = GetTags(node);
                        var examplesNode = node.GetSingle<AstNode>(RuleType.Examples);
                        var examplesLine = examplesNode.GetToken(TokenType.ExamplesLine);
                        var description = GetDescription(examplesNode);

                        var allRows = examplesNode.GetSingle<GherkinDocument.Types.Feature.Types.TableRow[]>(RuleType.ExamplesTable);
                        var header = allRows != null ? allRows.First() : null;
                        var rows = allRows != null ? allRows.Skip(1).ToArray() : null;
                        return CreateExamples(tags, GetLocation(examplesLine), examplesLine.MatchedKeyword, examplesLine.MatchedText, description, header, rows, node);
                    }
                case RuleType.ExamplesTable:
                    {
                        return GetTableRows(node);
                    }
                case RuleType.Description:
                    {
                        var lineTokens = node.GetTokens(TokenType.Other);

                        // Trim trailing empty lines
                        lineTokens = lineTokens.Reverse().SkipWhile(t => string.IsNullOrWhiteSpace(t.MatchedText)).Reverse();

                        return string.Join(Environment.NewLine, lineTokens.Select(lt => lt.MatchedText));
                    }
                case RuleType.Feature:
                    {
                        var header = node.GetSingle<AstNode>(RuleType.FeatureHeader);
                        if (header == null) return null;
                        var tags = GetTags(header);
                        var featureLine = header.GetToken(TokenType.FeatureLine);
                        if (featureLine == null) return null;
                        var children = new List<GherkinDocument.Types.Feature.Types.FeatureChild>();
                        var background = node.GetSingle<GherkinDocument.Types.Feature.Types.Background>(RuleType.Background);
                        if (background != null)
                        {
                            children.Add(new GherkinDocument.Types.Feature.Types.FeatureChild() { Background = background });
                        }
                        var childrenEnumerable = children.Concat(node.GetItems<GherkinDocument.Types.Feature.Types.Scenario>(RuleType.ScenarioDefinition)
                                .Select(i => new GherkinDocument.Types.Feature.Types.FeatureChild()
                                {
                                    Scenario = i
                                }))
                                .Concat(node.GetItems<GherkinDocument.Types.Feature.Types.FeatureChild.Types.Rule>(RuleType.Rule)
                                    .Select(i => new GherkinDocument.Types.Feature.Types.FeatureChild()
                                    {
                                        Rule = i
                                    }));
                        var description = GetDescription(header);
                        if (featureLine.MatchedGherkinDialect == null) return null;
                        var language = featureLine.MatchedGherkinDialect.Language;

                        return CreateFeature(tags, GetLocation(featureLine), language, featureLine.MatchedKeyword, featureLine.MatchedText, description, childrenEnumerable.ToArray(), node);
                    }
                case RuleType.Rule:
                    {
                        var header = node.GetSingle<AstNode>(RuleType.RuleHeader);
                        if (header == null) return null;
                        var ruleLine = header.GetToken(TokenType.RuleLine);
                        if (ruleLine == null) return null;
                        var children = new List<GherkinDocument.Types.Feature.Types.FeatureChild.Types.RuleChild>();
                        var background = node.GetSingle<GherkinDocument.Types.Feature.Types.Background>(RuleType.Background);
                        if (background != null)
                        {
                            children.Add(new GherkinDocument.Types.Feature.Types.FeatureChild.Types.RuleChild()
                            {
                                Background = background
                            });
                        }
                        var childrenEnumerable = children.Concat(node.GetItems<GherkinDocument.Types.Feature.Types.Scenario>(RuleType.ScenarioDefinition).Select(i => new GherkinDocument.Types.Feature.Types.FeatureChild.Types.RuleChild()
                        {
                            Scenario = i
                        }));
                        var description = GetDescription(header);
                        if (ruleLine.MatchedGherkinDialect == null) return null;
                        var language = ruleLine.MatchedGherkinDialect.Language;

                        return CreateRule(GetLocation(ruleLine), ruleLine.MatchedKeyword, ruleLine.MatchedText, description, childrenEnumerable.ToArray(), node);
                    }
                case RuleType.GherkinDocument:
                    {
                        var feature = node.GetSingle<GherkinDocument.Types.Feature>(RuleType.Feature);

                        return CreateGherkinDocument(feature, comments.ToArray(), node);
                    }
            }

            return node;
        }

        protected virtual GherkinDocument.Types.Feature.Types.Background CreateBackground(Location location, string keyword, string name, string description, GherkinDocument.Types.Feature.Types.Step[] steps, AstNode node)
        {
            var background = new GherkinDocument.Types.Feature.Types.Background()
            {
                Location = location,
                Keyword = keyword,
                Name = name,
                Description = description
            };

            foreach (var step in steps)
            {
                background.Steps.Add(step);
            }

            return background;
        }

        protected virtual GherkinDocument.Types.Feature.Types.Step.Types.DataTable CreateDataTable(GherkinDocument.Types.Feature.Types.TableRow[] rows, AstNode node)
        {
            var dataTable = new GherkinDocument.Types.Feature.Types.Step.Types.DataTable();

            foreach (var row in rows)
            {
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        protected virtual GherkinDocument.Types.Comment CreateComment(Location location, string text)
        {
            return new GherkinDocument.Types.Comment()
            {
                Location = location,
                Text = text
            };
        }

        protected virtual GherkinDocument.Types.Feature.Types.Scenario.Types.Examples CreateExamples(GherkinDocument.Types.Feature.Types.Tag[] tags, Location location, string keyword, string name, string description, GherkinDocument.Types.Feature.Types.TableRow header, GherkinDocument.Types.Feature.Types.TableRow[] body, AstNode node)
        {
            var examples = new GherkinDocument.Types.Feature.Types.Scenario.Types.Examples()
            {
                Location = location,
                Keyword = keyword,
                Name = name,
                Description = description,
                TableHeader = header
            };

            foreach (var tag in tags)
            {
                examples.Tags.Add(tag);
            }

            if (body != null)
            {
                foreach (var tableRow in body)
                {
                    examples.TableBody.Add(tableRow);
                }
            }

            return examples;
        }

        protected virtual GherkinDocument.Types.Feature.Types.Scenario CreateScenario(GherkinDocument.Types.Feature.Types.Tag[] tags, Location location, string keyword, string name, string description, GherkinDocument.Types.Feature.Types.Step[] steps, GherkinDocument.Types.Feature.Types.Scenario.Types.Examples[] examples, AstNode node)
        {
            var scenario = new GherkinDocument.Types.Feature.Types.Scenario()
            {
                Location = location,
                Keyword = keyword,
                Name = name,
                Description = description
            };


            foreach (var tag in tags)
            {
                scenario.Tags.Add(tag);
            }

            foreach (var example in examples)
            {
                scenario.Examples.Add(example);
            }

            foreach (var step in steps)
            {
                scenario.Steps.Add(step);
            }

            return scenario;
        }

        protected virtual GherkinDocument.Types.Feature.Types.Step.Types.DocString CreateDocString(Location location, string contentType, string content, AstNode node)
        {
            return new GherkinDocument.Types.Feature.Types.Step.Types.DocString()
            {
                Location = location,
                MediaType = contentType,
                Content = content
            };
        }

        protected virtual GherkinDocument.Types.Feature.Types.Step CreateStep(Location location, string keyword, string text, GherkinDocument.Types.Feature.Types.Step.Types.DocString docString, GherkinDocument.Types.Feature.Types.Step.Types.DataTable dataTable, AstNode node)
        {
            var step = new GherkinDocument.Types.Feature.Types.Step()
            {
                Location = location,
                Keyword = keyword,
                Text = text,
                DataTable = dataTable,
                DocString = docString
            };

            return step;
        }

        protected virtual GherkinDocument CreateGherkinDocument(GherkinDocument.Types.Feature feature, GherkinDocument.Types.Comment[] gherkinDocumentComments, AstNode node)
        {
            return new GherkinDocument()
            {
                Feature = feature

            };
        }

        protected virtual GherkinDocument.Types.Feature CreateFeature(GherkinDocument.Types.Feature.Types.Tag[] tags, Location location, string language, string keyword, string name, string description, GherkinDocument.Types.Feature.Types.FeatureChild[] children, AstNode node)
        {
            var feature = new GherkinDocument.Types.Feature()
            {
                Location = location,
                Language = language,
                Keyword = keyword,
                Name = name,
                Description = description
            };


            foreach (var child in children)
            {
                feature.Children.Add(child);
            }

            foreach (var tag in tags)
            {
                feature.Tags.Add(tag);
            }

            return feature;
        }

        protected virtual GherkinDocument.Types.Feature.Types.FeatureChild.Types.Rule CreateRule(Location location, string keyword, string name, string description, GherkinDocument.Types.Feature.Types.FeatureChild.Types.RuleChild[] children, AstNode node)
        {
            var rule = new GherkinDocument.Types.Feature.Types.FeatureChild.Types.Rule()
            {
                Location = location,
                Keyword = keyword,
                Name = name,
                Description = description
            };


            foreach (var child in children)
            {
                rule.Children.Add(child);
            }

            return rule;
        }

        protected virtual GherkinDocument.Types.Feature.Types.Tag CreateTag(Location location, string name, AstNode node)
        {
            return new GherkinDocument.Types.Feature.Types.Tag()
            {
                Location = location,
                Name = name
            };
        }

        protected virtual Location CreateLocation(uint line, uint column)
        {
            return new Location()
            {
                Line = line,
                Column = column
            };
        }

        protected virtual GherkinDocument.Types.Feature.Types.TableRow CreateTableRow(Location location, GherkinDocument.Types.Feature.Types.TableRow.Types.TableCell[] cells, AstNode node)
        {
            var tableRow = new GherkinDocument.Types.Feature.Types.TableRow()
            {
                Location = location
            };

            foreach (var cell in cells)
            {
                tableRow.Cells.Add(cell);
            }
            return tableRow;
        }

        protected virtual GherkinDocument.Types.Feature.Types.TableRow.Types.TableCell CreateTableCell(Location location, string value)
        {
            return new GherkinDocument.Types.Feature.Types.TableRow.Types.TableCell()
            {
                Location = location,
                Value = value
            };
        }

        private Location GetLocation(Token token, uint column = 0)
        {
            return column == 0 ? token.Location : CreateLocation(token.Location.Line, column);
        }

        private GherkinDocument.Types.Feature.Types.Tag[] GetTags(AstNode node)
        {
            var tagsNode = node.GetSingle<AstNode>(RuleType.Tags);
            if (tagsNode == null)
                return new GherkinDocument.Types.Feature.Types.Tag[0];

            return tagsNode.GetTokens(TokenType.TagLine)
                .SelectMany(t => t.MatchedItems, (t, tagItem) =>
                    CreateTag(GetLocation(t, tagItem.Column), tagItem.Text, tagsNode))
                .ToArray();
        }

        private GherkinDocument.Types.Feature.Types.TableRow[] GetTableRows(AstNode node)
        {
            var rows = node.GetTokens(TokenType.TableRow).Select(token => CreateTableRow(GetLocation(token), GetCells(token), node)).ToArray();
            CheckCellCountConsistency(rows);
            return rows;
        }

        protected virtual void CheckCellCountConsistency(GherkinDocument.Types.Feature.Types.TableRow[] rows)
        {
            if (rows.Length == 0)
                return;

            int cellCount = rows[0].Cells.Count();
            foreach (var row in rows)
            {
                if (row.Cells.Count() != cellCount)
                {
                    HandleAstError("inconsistent cell count within the table", row.Location);
                }
            }
        }

        protected virtual void HandleAstError(string message, Location location)
        {
            throw new AstBuilderException(message, location);
        }

        private GherkinDocument.Types.Feature.Types.TableRow.Types.TableCell[] GetCells(Token tableRowToken)
        {
            return tableRowToken.MatchedItems
                .Select(cellItem => CreateTableCell(GetLocation(tableRowToken, cellItem.Column), cellItem.Text))
                .ToArray();
        }

        private static GherkinDocument.Types.Feature.Types.Step[] GetSteps(AstNode scenarioDefinitionNode)
        {
            return scenarioDefinitionNode.GetItems<GherkinDocument.Types.Feature.Types.Step>(RuleType.Step).ToArray();
        }

        private static string GetDescription(AstNode scenarioDefinitionNode)
        {
            return scenarioDefinitionNode.GetSingle<string>(RuleType.Description) ?? String.Empty;
        }
    }
}
