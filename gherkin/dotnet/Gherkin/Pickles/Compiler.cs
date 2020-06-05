using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Google.Protobuf.Collections;
using Io.Cucumber.Messages;

// ReSharper disable PossibleMultipleEnumeration

namespace Gherkin.Pickles
{
    public class Compiler
    {
        public List<Pickle> Compile(GherkinDocument gherkinDocument)
        {
            var pickles = new List<Pickle>();
            GherkinDocument.Types.Feature feature = gherkinDocument.Feature;
            if (feature == null)
            {
                return pickles;
            }

            var language = feature.Language;
            var tags = feature.Tags;
            var backgroundSteps = new Pickle.Types.PickleStep[0];

            Build(pickles, language, tags, backgroundSteps, feature);

            return pickles;
        }

        protected virtual void Build(List<Pickle> pickles, string language, IEnumerable<GherkinDocument.Types.Feature.Types.Tag> tags, IEnumerable<Pickle.Types.PickleStep> parentBackgroundSteps, GherkinDocument.Types.Feature feature)
        {
            IEnumerable<Pickle.Types.PickleStep> backgroundSteps = new List<Pickle.Types.PickleStep>(parentBackgroundSteps);
            foreach (var child in feature.Children)
            {
                if (child.Background != null)
                {
                    backgroundSteps = backgroundSteps.Concat(PickleSteps(child.Background.Steps));
                }

                if (child.Rule != null)
                {
                    Build(pickles, language, tags, backgroundSteps, child.Rule.Children);
                }

                if (child.Scenario != null)
                {
                    var scenario = child.Scenario;
                    if (!scenario.Examples.Any())
                    {
                        CompileScenario(pickles, backgroundSteps, scenario, tags, language);
                    }
                    else
                    {
                        CompileScenarioOutline(pickles, backgroundSteps, scenario, tags, language);
                    }
                }


            }
        }

        protected virtual void Build(List<Pickle> pickles, string language,
            IEnumerable<GherkinDocument.Types.Feature.Types.Tag> tags,
            IEnumerable<Pickle.Types.PickleStep> parentBackgroundSteps,
            RepeatedField<GherkinDocument.Types.Feature.Types.FeatureChild.Types.RuleChild> ruleChildren)
        {
            IEnumerable<Pickle.Types.PickleStep> backgroundSteps = new List<Pickle.Types.PickleStep>(parentBackgroundSteps);
            foreach (var child in ruleChildren)
            {
                if (child.Background != null)
                {
                    backgroundSteps = backgroundSteps.Concat(PickleSteps(child.Background.Steps));
                }
                
                if (child.Scenario != null)
                {
                    var scenario = child.Scenario;
                    if (!scenario.Examples.Any())
                    {
                        CompileScenario(pickles, backgroundSteps, scenario, tags, language);
                    }
                    else
                    {
                        CompileScenarioOutline(pickles, backgroundSteps, scenario, tags, language);
                    }
                }


            }
        }

        private IEnumerable<Pickle.Types.PickleStep> PickleSteps(RepeatedField<GherkinDocument.Types.Feature.Types.Step> scenarioDefinition)
        {
            return scenarioDefinition.Select(PickleStep);
        }

        protected virtual void CompileScenario(List<Pickle> pickles, IEnumerable<Pickle.Types.PickleStep> backgroundSteps, GherkinDocument.Types.Feature.Types.Scenario scenario, IEnumerable<GherkinDocument.Types.Feature.Types.Tag> featureTags, string language)
        {
            var steps = new List<Pickle.Types.PickleStep>();
            if (scenario.Steps.Any())
                steps.AddRange(backgroundSteps);

            var scenarioTags = new List<GherkinDocument.Types.Feature.Types.Tag>();
            scenarioTags.AddRange(featureTags);
            scenarioTags.AddRange(scenario.Tags);

            steps.AddRange(PickleSteps(scenario));

            Pickle pickle = new Pickle()
            {
                Name = scenario.Name,
                Language = language,
            };

            foreach (var step in steps)
            {
                pickle.Steps.Add(step);
            }

            foreach (var pickleTag in PickleTags(scenarioTags))
            {
                pickle.Tags.Add(pickleTag);
            }


            pickles.Add(pickle);
        }

        protected virtual IEnumerable<T> SingletonList<T>(T item)
        {
            return new[] { item };
        }

        protected virtual void CompileScenarioOutline(List<Pickle> pickles, IEnumerable<Pickle.Types.PickleStep> backgroundSteps, GherkinDocument.Types.Feature.Types.Scenario scenarioOutline, IEnumerable<GherkinDocument.Types.Feature.Types.Tag> featureTags, string language)
        {
            foreach (var examples in scenarioOutline.Examples)
            {
                if (examples.TableHeader == null) continue;
                var variableCells = examples.TableHeader.Cells;
                foreach (var values in examples.TableBody)
                {
                    var valueCells = values.Cells;

                    var steps = new List<Pickle.Types.PickleStep>();
                    if (scenarioOutline.Steps.Any())
                        steps.AddRange(backgroundSteps);

                    var tags = new List<GherkinDocument.Types.Feature.Types.Tag>();
                    tags.AddRange(featureTags);
                    tags.AddRange(scenarioOutline.Tags);
                    tags.AddRange(examples.Tags);

                    foreach (var scenarioOutlineStep in scenarioOutline.Steps)
                    {
                        string stepText = Interpolate(scenarioOutlineStep.Text, variableCells, valueCells);

                        // TODO: Use an Array of location in DataTable/DocString as well.
                        // If the Gherkin AST classes supported
                        // a list of locations, we could just reuse the same classes

                        Pickle.Types.PickleStep pickleStep = CreatePickleStep(
                                scenarioOutlineStep,
                                stepText,
                                CreatePickleArguments(scenarioOutlineStep.DocString, scenarioOutlineStep.DataTable)
                        );
                        steps.Add(pickleStep);
                    }

                    Pickle pickle = new Pickle()
                    {
                        Language = language,
                        Name = scenarioOutline.Name,
                    };

                    foreach (var step in steps)
                    {
                        pickle.Steps.Add(step);
                    }

                    foreach (var tag in PickleTags(tags))
                    {
                        pickle.Tags.Add(tag);
                    }

                    pickles.Add(pickle);
                }
            }
        }

        protected virtual Pickle.Types.PickleStep CreatePickleStep(GherkinDocument.Types.Feature.Types.Step step, string text, PickleStepArgument argument)
        {
            return new Pickle.Types.PickleStep()
            {
                Text = text,
                Argument = argument

            };
        }

        protected virtual PickleStepArgument CreatePickleArguments(GherkinDocument.Types.Feature.Types.Step.Types.DocString docString, GherkinDocument.Types.Feature.Types.Step.Types.DataTable dataTable)
        {
            if (docString == null && dataTable == null)
            {
                return null;
            }

            if (docString != null)
            {

                return new PickleStepArgument()
                {
                    DocString = new PickleStepArgument.Types.PickleDocString()
                    {
                        Content = docString.Content,
                        MediaType = docString.MediaType
                    }
                };
            }

            if (dataTable != null)
            {
                var pickleStepArgument = new PickleStepArgument()
                {
                    DataTable = new PickleStepArgument.Types.PickleTable()
                    {

                    }
                };


                foreach (var row in dataTable.Rows)
                {
                    var pickleTableRow = new PickleStepArgument.Types.PickleTable.Types.PickleTableRow();


                    foreach (var cell in row.Cells)
                    {
                        pickleTableRow.Cells.Add(
                            new PickleStepArgument.Types.PickleTable.Types.PickleTableRow.Types.PickleTableCell()
                            {
                                Value = cell.Value
                            });
                    }

                    pickleStepArgument.DataTable.Rows.Add(pickleTableRow);
                }

                return pickleStepArgument;
            }

            return null;
        }


        protected virtual Pickle.Types.PickleStep[] PickleSteps(GherkinDocument.Types.Feature.Types.Scenario scenarioDefinition)
        {
            var result = new List<Pickle.Types.PickleStep>();


            foreach (var step in scenarioDefinition.Steps)
            {
                result.Add(PickleStep(step));
            }
            return result.ToArray();
        }

        protected virtual Pickle.Types.PickleStep PickleStep(GherkinDocument.Types.Feature.Types.Step step)
        {
            return CreatePickleStep(
                    step,
                    step.Text,
                    CreatePickleArguments(step.DocString, step.DataTable)
            );
        }

        protected virtual string Interpolate(string name, IEnumerable<GherkinDocument.Types.Feature.Types.TableRow.Types.TableCell> variableCells, IEnumerable<GherkinDocument.Types.Feature.Types.TableRow.Types.TableCell> valueCells)
        {
            int col = 0;
            foreach (var variableCell in variableCells)
            {
                GherkinDocument.Types.Feature.Types.TableRow.Types.TableCell valueCell = valueCells.ElementAt(col++);
                string header = variableCell.Value;
                string value = valueCell.Value;
                name = name.Replace("<" + header + ">", value);
            }
            return name;
        }



        protected virtual List<Pickle.Types.PickleTag> PickleTags(List<GherkinDocument.Types.Feature.Types.Tag> tags)
        {
            var result = new List<Pickle.Types.PickleTag>();
            foreach (var tag in tags)
            {
                result.Add(PickleTag(tag));
            }
            return result;
        }

        protected virtual Pickle.Types.PickleTag PickleTag(GherkinDocument.Types.Feature.Types.Tag tag)
        {
            return new Pickle.Types.PickleTag()
            {
                Name = tag.Name
            };
        }
    }
}
