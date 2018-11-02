using FluentAssertions;
using Gherkin.CLI;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Gherkin.Specs
{
    public class PicklesTests
    {
        [Theory, MemberData(nameof(TestFileProvider.GetValidTestFiles), MemberType = typeof(TestFileProvider))]
        public void TestSuccessfulPickles(string testFeatureFile)
        {
            var featureFileFolder = Path.GetDirectoryName(testFeatureFile);
            Debug.Assert(featureFileFolder != null);
            var expectedTokensFile = testFeatureFile + ".pickles.ndjson";

            var expectedTokensText = LineEndingHelper.NormalizeLineEndings(File.ReadAllText(expectedTokensFile));

            var expected = DeserializeNDJson(expectedTokensText);


            var output = new StringBuilder();
            var jsonSerializerSettings = JsonSerializationSettings.CreateJsonSerializerSettings();
            SourceEvents sourceEvents = new SourceEvents(new List<string>() { testFeatureFile });
            GherkinEvents gherkinEvents = new GherkinEvents(false, false, true);
            foreach (SourceEvent sourceEventEvent in sourceEvents)
            {
                foreach (IEvent evt in gherkinEvents.iterable(sourceEventEvent))
                {
                    if (evt is PickleEvent pe)
                    {
                        var serializeObject = JsonConvert.SerializeObject(new
                        {
                            pickle = new
                            {
                                language = pe.pickle.Language,
                                locations = pe.pickle.Locations,
                                name = pe.pickle.Name,
                                steps = pe.pickle.Steps,
                                uri = pe.uri
                            },

                        }, jsonSerializerSettings);


                        output.AppendLine(serializeObject);
                    }
                }
            }

            var actual = DeserializeNDJson(output.ToString());

            var result = JsonDiff.CompareArrays(expected, actual, new string[]{"uri"});

            result.ToString().Should().BeNullOrWhiteSpace();
        }

        private static JArray DeserializeNDJson(string expectedTokensText)
        {
            JArray expected = new JArray();
            foreach (var s in expectedTokensText.Split(new char[]{ '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                expected.Add(JToken.Parse(s));
            }

            return expected;
        }
    }

    public class JsonDiff
    {
        /// <summary>
        /// Deep compare two NewtonSoft JObjects. If they don't match, returns text diffs
        /// </summary>
        /// <param name="source">The expected results</param>
        /// <param name="target">The actual results</param>
        /// <returns>Text string</returns>

        public static StringBuilder CompareObjects(JObject expected, JObject actual, string[] ignoreValuesOfProperties)
        {
            var result = new StringBuilder();

            var sortedExpected = expected.OrderBy((KeyValuePair<string, JToken> x) => x.Key).ToList();
            var sortedActual = actual.OrderBy((KeyValuePair<string, JToken> x) => x.Key).ToList();

            for (int i = 0; i < sortedExpected.Count(); i++)
            {
                bool foundActualProperty = false;

                var expectedProperty = sortedExpected[i];

                foreach (var actualProperty in sortedActual)
                {
                    if (expectedProperty.Key == actualProperty.Key)
                    {
                        foundActualProperty = true;
                    }
                    else
                    {
                        continue;
                    }

                    if (ignoreValuesOfProperties.Contains(expectedProperty.Key))
                    {
                        break;
                    }

                    switch (expectedProperty.Value)
                    {
                        case JValue expectedJValue:
                            var actualValue = ((JValue)actualProperty.Value).Value<string>();

                            var expectedValue = expectedJValue.Value<string>();
                            if (expectedValue != actualValue)
                            {
                                result.AppendLine(
                                    $"{expectedProperty.Key}: Expected {expectedValue}; Actual: {actualValue}");
                            }
                            break;
                        case JArray expectedJArray:
                            {
                                var actualArrayValue = (JArray)actualProperty.Value;
                                var recursive = CompareArrays(expectedJArray, actualArrayValue, ignoreValuesOfProperties);
                                result.AppendLine(recursive.ToString());
                            }
                            break;
                        case JObject expectedJObject:
                            {
                                var actualPropertyValue = (JObject)actualProperty.Value;
                                var recursive = CompareObjects(expectedJObject, actualPropertyValue, ignoreValuesOfProperties);
                                result.AppendLine(recursive.ToString());
                            }
                            break;

                    }
                }

                if (!foundActualProperty)
                {
                    result.AppendLine($"{expectedProperty.Key} not found");
                }
            }


            return result;
        }

        /// <summary>
        /// Deep compare two NewtonSoft JArrays. If they don't match, returns text diffs
        /// </summary>
        /// <param name="source">The expected results</param>
        /// <param name="target">The actual results</param>
        /// <param name="arrayName">The name of the array to use in the text diff</param>
        /// <returns>Text string</returns>

        public static StringBuilder CompareArrays(JArray source, JArray target, string[] ignoreValuesOfProperties, string arrayName = "")
        {
            var returnString = new StringBuilder();

            var sortedSource = source.OrderBy(x => x.ToString(Formatting.None)).ToList();
            var sortedTarget = target.OrderBy(x => x.ToString(Formatting.None)).ToList();

            for (var index = 0; index < sortedSource.Count; index++)
            {



                var expected = sortedSource[index];
                if (expected.Type == JTokenType.Object)
                {
                    var actual = (index >= sortedTarget.Count) ? new JObject() : sortedTarget[index];
                    returnString.Append(CompareObjects(expected.ToObject<JObject>(), actual.ToObject<JObject>(), ignoreValuesOfProperties));
                }
                else
                {

                    var actual = (index >= sortedTarget.Count) ? "" : sortedTarget[index];
                    if (!JToken.DeepEquals(expected, actual))
                    {
                        if (String.IsNullOrEmpty(arrayName))
                        {
                            returnString.Append("Index " + index + ": " + expected
                                                + " != " + actual + Environment.NewLine);
                        }
                        else
                        {
                            returnString.Append("Key " + arrayName
                                                + "[" + index + "]: " + expected
                                                + " != " + actual + Environment.NewLine);
                        }
                    }
                }
            }
            return returnString;
        }
    }
}