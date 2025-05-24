using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.

namespace TaxonClassifierLib
{
    public class ClassifyClass
    {
        public static string special_characters = "ẅëẗÿüïöäḧẍẃéŕýúíóṕǻáśǵḱĺǽǿźćǘńḿẁèỳùìòàùǹåæøẽỹũĩõãṽñŵêŷûîôâŝĝĥĵẑĉþœßŋħĸłµçð";

        // A stepping stone so external programs do not need JObject 
        public static string classifyText(string text, string taxonomyJSON, Dictionary<string, string> settingsDic)
        {
            JObject result = new JObject();

            JObject taxonomy = JObject.Parse(taxonomyJSON);

            JObject settings = new JObject();

            foreach(KeyValuePair<string, string> setting in settingsDic)
            {
                settings[setting.Key] = setting.Value;
            }

            result = classify(text, taxonomy, settings);

            return result.ToString();
        }

        public static JObject classify(string text, JObject taxonomy, JObject settings)
        {
            JObject result = new JObject();
            JObject classes = new JObject();

            // We expect everything to go well
            result["status"] = "OK";

            // Remove HTML "chars"
            //            text = Regex.Replace(text, "&[a-z]{2,5};", " ", RegexOptions.IgnoreCase);
            text = HttpUtility.HtmlDecode(text);

            // Remove all non-letters
            string skip_letters = " \\.,;:\\-_!\\?§0-9a-z" + special_characters;

            text = Regex.Replace(text, "[^" + skip_letters + "]+", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "[ ]+", " ");

            if (text.Length > 50000)
            {
                text = text.Substring(0, 50000);
            }

            if (text == "")
            {
                result["status"] = "Error";
                result["status_code"] = "20";
                result["status_message"] = "No text was provided";

                return result;
            }

            try
            {
                if (taxonomy["system"]["versions"]["taxon_version"].ToString() != "3.x")
                {
                    result["status"] = "Error";
                    result["status_code"] = "100";
                    result["status_message"] = "Taxon version expected '3.x' but found '" + taxonomy["system"]["versions"]["taxon_version"] + "'";

                    return result;
                }
            }
            catch
            {
                result["status"] = "Error";
                result["status_code"] = "110";
                result["status_message"] = "Taxon version not set";

                return result;
            }

            // Set some defaults
            int numberResultsReturned = 5;
            int onNoResultsIgnoreTermConstraints = 0;
            int onNoResultsUseAlternativeTaxonomy = 0;
            int returnShortResult = 0;
            SortedList<string, string> alterTextList = new SortedList<string, string>();
            int returnOnlyFirstClassFoundByPosition = 0;
            string analysisMethod = "standard";
            string scoreCalculationMethod = "standard";
            string confidenceCoefficientCalculationMethod = "standard";
            int firstClassExtraWeight = 0;
            int scoreTotalThreshold = 0;
            int confidenceCoefficientThreshold = 0;


            /************** Handle settings, whether they come from the defaults in the taxonomy or as parameters in $settings ******/
            // Start classification
            bool classification_done = false;
            string classification_method = "Full classification";

            try
            {
                if (settings["numberResultsReturned"] == null)
                {
                    if (taxonomy["system"]["classification"]["numberResultsReturned"] != null)
                    {
                        if (Int32.TryParse(taxonomy["system"]["classification"]["numberResultsReturned"].ToString(), out numberResultsReturned) == false)
                        {
                            numberResultsReturned = 5;
                        }
                    }
                }
                else
                {
                    numberResultsReturned = (int)settings["numberResultsReturned"];
                }

                if (taxonomy["system"]["score"]["firstClassExtraWeight"] != null)
                {
                    if (Int32.TryParse(taxonomy["system"]["score"]["firstClassExtraWeight"].ToString(), out firstClassExtraWeight) == false)
                    {
                        firstClassExtraWeight = 10;
                    }
                }

                if (taxonomy["system"]["score"]["scoreTotalThreshold"] != null)
                {
                    if (Int32.TryParse(taxonomy["system"]["score"]["scoreTotalThreshold"].ToString(), out scoreTotalThreshold) == false)
                    {
                        scoreTotalThreshold = 30;
                    }
                }

                if (taxonomy["system"]["score"]["confidenceCoefficientThreshold"] != null)
                {
                    if (Int32.TryParse(taxonomy["system"]["score"]["confidenceCoefficientThreshold"].ToString(), out confidenceCoefficientThreshold) == false)
                    {
                        confidenceCoefficientThreshold = 30;
                    }
                }


                // Apply alterText
                JArray alterTexts = Newtonsoft.Json.JsonConvert.DeserializeObject<JArray>(taxonomy["system"]["alterText"].ToString());

                if (alterTexts.Count > 0)
                {
                    foreach (JObject alterText in alterTexts)
                    {
                        foreach (JProperty field in (JToken)alterText)
                        {
                            string from = field.Name;
                            string to = alterText[field.Name].ToString();

                            text = Regex.Replace(text, from, to, RegexOptions.IgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                Debug.WriteLine("CATCH Classifier 111");
            }

            // No more changes are made to the text so convert the text and the taxonomy name to lower for UTF8 characters as well
            text = " " + text.ToLower() + " ";

            // Do the actual classification
            try
            {
                classes = classify_full(taxonomy, text, settings, classification_method, analysisMethod);
            }
            catch (Exception e)
            {
                Debug.WriteLine("CATCH Classifier 124 " + e);
            }

            // Get the score for each class.
            // Note: $classes is passed by reference
            try
            {
                calculateScores(ref classes, firstClassExtraWeight, scoreCalculationMethod);
            }
            catch
            {
                Debug.WriteLine("CATCH Classifier 135");
            }

            // Sort the results.
            // We sort according to the weight and the firstposition.
            try
            {
                sortClassesByWeight(ref classes);
            }
            catch
            {
                Debug.WriteLine("CATCH Classifier 146");
            }

            // Calculate the confidence coefficient
            // Note: $classes is passed by reference
            try
            {
                calculateConfidenceCoefficient(ref classes, confidenceCoefficientCalculationMethod);
            }
            catch
            {
                Debug.WriteLine("CATCH Classifier 157");
            }

            //  Check scoreTotalThreshold and confidenceCoefficientThreshold to
            //  whether we should return an result.
            //  Note: If scoreTotal is too low, all the classes are removed and the 
            //  check for scoreConfidenceCoefficient is not done.

            try
            {
                if (scoreTotalThreshold > 0)
                {
                    // Check the totalScore of the top class
                    if (classes.Count > 0)
                    {
                        JProperty first_class = (JProperty)classes.First;

                        int scoreTotal = Int32.Parse(first_class.Value["scoreTotal"].ToString());

                        if (scoreTotal < scoreTotalThreshold)
                        {
                            result["status"] = "Information";
                            result["status_code"] = "1000";

                            string message = "";

                            message += "scoreTotal (" + scoreTotal + ") is smaller than scoreTotalThreshold(" + scoreTotalThreshold + ")" + Environment.NewLine + Environment.NewLine;

                            message += "Class ID = " + first_class.Name + " " + first_class.Value["title"].ToString() + Environment.NewLine;

                            try
                            {
                                foreach (JProperty term in first_class.Value["terms"])
                                {
                                    message += "   " + term.Name + ": " + term.Value["scoreWeight"].ToString() + Environment.NewLine;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("236 " + ex.Message);
                            }

                            result["status_message"] = message;

                            return result;
                        }
                    }
                }

                if (confidenceCoefficientThreshold > 0)
                {
                    // Check the totalScore of the top class
                    if (classes.Count > 0)
                    {
                        JProperty first_class = (JProperty)classes.First;

                        int scoreConfidenceCoefficient = Int32.Parse(first_class.Value["scoreConfidenceCoefficient"].ToString());

                        if (scoreConfidenceCoefficient < confidenceCoefficientThreshold)
                        {
                            result["status"] = "Information";
                            result["status_code"] = "1010";

                            string message = "";

                            message += "scoreConfidenceCoefficient (" + scoreConfidenceCoefficient + ") is smaller than confidenceCoefficientThreshold (" + confidenceCoefficientThreshold + ")" + Environment.NewLine + Environment.NewLine;

                            try
                            {
                                int max_classes = 2;

                                foreach (JProperty classid in (JToken)classes)
                                {
                                    message += classid.Name + " " + classid.Value["title"].ToString() + "  (score = " + classid.Value["scoreTotal"] + ")" + Environment.NewLine;

                                    foreach (JProperty term in classid.Value["terms"])
                                    {
                                        message += "   " + term.Name + ": " + term.Value["scoreWeight"].ToString() + Environment.NewLine;
                                    }

                                    message += "   " + Environment.NewLine;

                                    if (--max_classes <= 0)
                                    {
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("278 " + ex.Message);
                            }

                            result["status_message"] = message;

                            return result;
                        }
                    }
                }

                // Return only the first class found by position.

                if (returnOnlyFirstClassFoundByPosition != 0)
                {
                    /*
                                    firstClassID = "";
                                    firstClassPosition = 999999999;

                                    foreach (classes as classid => class)
                                    {
                                        if(class['scoreFirstPosition'] < firstClassPosition)
                                        {
                                            firstClassID = classid;
                                            firstClassPosition = class['scoreFirstPosition'];
                                        }
                                    }

                                    if(firstClassID != "")
                                    {
                                        // We did find a class.
                                        // Remove all other classes and keep only the found class.
                                        firstClass = classes[firstClassID];

                                        classes = array();

                                        classes[firstClassID] = firstClass;
                                    }

                                    status['status'] = "OK";
                                    status['informations'][] = array(
                                        "informationCode" => "1020",
                                        "informationText" => "returnOnlyFirstClassFoundByPosition was applied",

                                    );
                    */
                }

                // Return the max number of result.

                // 0 means return all
                if (numberResultsReturned > 0)
                {
                    int count = classes.Count;

                    int position = 1;

                    IList<string> keys = classes.Properties().Select(p => p.Name).ToList();

                    foreach (JProperty tclass in (JToken)classes)
                    {
                        // Remove the key from the list of keys. Keys in the list will be removed
                        if (position <= numberResultsReturned)
                        {
                            keys.Remove(tclass.Name.ToString());
                        }

                        position++;
                    }

                    // Remove the classes with the key in keys
                    foreach (string key in keys)
                    {
                        classes.Remove(key);
                    }

                    if (numberResultsReturned < count)
                    {
                        result["status"] = "Information";
                        result["status_code"] = "1030";
                        result["status_message"] = "numberResultsReturned (" + numberResultsReturned + ") was applied. Removed " + (count - numberResultsReturned) + " classes";
                    }
                }
            }
            catch
            {
                Debug.WriteLine("CATCH Classifier 282");
            }

            result["classification_status"] = "OK";

            if (classes.Count > 0)
            {
                result["status"] = "id";

                result["topclass"] = classes.First.ToObject<JProperty>().Name.ToString();
            }
            else
            {
                result["status"] = "ok";
            }

            result["classes"] = classes;

            return result;
        }

        ///////////////////////////////////////////////////////////
        //	Internal functions
        ///////////////////////////////////////////////////////////
        public static JObject classify_full(JObject taxonomy_lookup, string text, JObject settings, string classification_method, string analysisMethod)
        {
            //            Stopwatch sw = new Stopwatch();

            //            sw.Restart();

            // Set the settings
            int ignoreTermConstraints = 0;

            if (settings["ignoreTermConstraints"] != null)
            {
                ignoreTermConstraints = (int)settings["ignoreTermConstraints"];
            }

            int ignoreClassConstraints = 0;

            if (settings["ignoreClassConstraints"] != null)
            {
                ignoreClassConstraints = (int)settings["ignoreClassConstraints"];
            }

            // As we find the terms and classes, keep them.
            JObject classes = new JObject();
            JObject classes_sentence = new JObject();
            JObject required = new JObject();
            JObject requiredOr = new JObject();

            JObject grammar = new JObject();

            string genitive = "s";

            try
            {
                genitive = taxonomy_lookup["system"]["grammar"]["genitive"].ToString();
            }
            catch (Exception e)
            {
                // Do nothing
                Debug.WriteLine("CATCH Classifier 270 " + e);
            }

            if (settings["genitive"] != null)
            {
                genitive = settings["genitive"].ToString();
            }


            grammar["genitive"] = genitive;

            //	Step 1: Find the terms in the text. 
            //	Build a list of required and requiredOr terms as we are going along.

            // First split the text into sentences
            // A sentence is a string of text ending with either .!?

            //            Debug.WriteLine("300 - new");

            JObject sentences = new JObject();

            switch (analysisMethod)
            {
                case "standard":
                    // Use the whole text as a sentence
                    sentences["standard"] = text;

                    break;
                /*
                        case "sentenceCentric":
                            if((preg_match_all("/([^\.\!\?]+)/u", $text, $matches)))
                            {
                                $sentences = $matches[1];
                            }
                            else
                            {
                                // Use the whole text as a sentence
                                $sentences[] = $text;
                            }
                            break;
                */
                default:
                    // Use the whole text as a sentence
                    sentences["default"] = text;

                    break;
            }

            foreach (JProperty sentence_obj in (JToken)sentences)
            {
                string sentence = sentence_obj.Value.ToString();

                //                sw.Restart();

                sentence = " " + sentence.Trim() + " ";
                // Note: passed by reference
                getClassesAndTerms(sentence, taxonomy_lookup, ref classes_sentence, required, requiredOr, grammar);

                //                sw.Restart();

                //	Step 2: Clean up terms.
                // Note: $classes is passed by reference
                cleanupTerms(ref classes_sentence);

                //                sw.Restart();

                //	Step 3: Check the require and exclude conditions.
                // Note: $classes, $taxonomy_lookup, $text are passed by reference
                if (ignoreTermConstraints == 0)
                {
                    applyTermConstraints(ref classes_sentence, taxonomy_lookup, classification_method, required, requiredOr, sentence);
                }

                //                sw.Restart();

                //	Step 4: Clean up classes.
                // Note: $classes is passed by reference
                cleanupClasses(ref classes_sentence);

                //                sw.Restart();

                //	Step 5: Check the require and exclude conditions.
                // Note: $classes is passed by reference
                if (ignoreClassConstraints == 0)
                {
                    applyClassConstraints(ref classes_sentence, sentence);
                }

                //                sw.Restart();

                // Array union
                foreach (JProperty classid_obj in (JToken)classes_sentence)
                {
                    string classid = classid_obj.Name.ToString();
                    JToken tclass = classes_sentence[classid];

                    classes[classid] = tclass;
                }

                //                sw.Restart();
            }

            return classes;
        }

        // Get the terms from the taxonomy that are in the text. Get the classes of the found terms
        private static void getClassesAndTerms(string text, JObject taxonomy_lookup, ref JObject classes, JObject required, JObject requiredOr, JObject grammar)
        {
            string term_title;
            JToken term = new JObject();

            Stopwatch sw = new Stopwatch();

            foreach (JProperty term_title_obj in (JToken)taxonomy_lookup["classes"])
            {
                term_title = term_title_obj.Name.ToString();
                term = taxonomy_lookup["classes"][term_title];

                bool regexp = false;

                sw.Restart();

                // Add term to the list of required and/or requiredOr lists
                // NOTE: This must be before the actual search for terms as this 
                // must be done for all classes in all terms
                foreach (JProperty classid_obj in (JToken)term["cs"])
                {
                    try
                    {
                        string classid = classid_obj.Name.ToString();
                        JToken tclass = term["cs"][classid];

                        if (Int32.Parse(tclass["r"].ToString()) == 1)
                        {
                            if (required[classid] == null)
                            {
                                required[classid] = new JObject();
                            }

                            required[classid][term_title] = 1;
                        }

                        if (Int32.Parse(tclass["ro"].ToString()) == 1)
                        {
                            if (requiredOr[classid] == null)
                            {
                                requiredOr[classid] = new JObject();
                            }

                            requiredOr[classid][term_title] = 1;
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("CATCH 420: " + term_title + ":" + classid_obj + ":");
                    }
                }

                // Must have a default value. In case something goes
                // wrong "" matches everything
                string matching_exp = "no match";

                // Do we have a regexp
                if (term_title.Substring(0, 1) == "/")
                {
                    // Get the regexp
                    matching_exp = term_title.Replace("/", "");

                    matching_exp = "(?<=[^a-z0-9" + special_characters + "\\;_\\-])" + matching_exp + grammar["genitive"] + "?(?=[^a-z0-9" + special_characters + "\\&_\\-])";

                    regexp = true;
                }
                else
                {
                    // Simple text search for the term in the text
                    // $text and $term_title must be lowercase.

                    if (text.Contains(term_title))
                    //                        if (text.IndexOf(term_title) >= 0)
                    {
                        string term_prefix = term["p"].ToString() != "" ? "(" + term["p"].ToString().ToLower() + ")?" : "";
                        string term_suffix = term["s"].ToString() != "" ? "(" + term["s"].ToString().ToLower() + ")?" : "";

                        // Disarm special chars
                        matching_exp = term_prefix + term_title + term_suffix;

                        matching_exp = "(?<=[^a-z0-9" + special_characters + "\\;_\\-])(" + matching_exp + grammar["genitive"] + "?)(?=[^a-z0-9" + special_characters + "\\&_\\-])";
                    }
                    else
                    {
                        continue;
                    }
                }

                //                Debug.WriteLine("t20 " + sw.ElapsedMilliseconds);
                //                sw.Restart();

                // Does the exact term exists in the text? Get all occurences
                MatchCollection matches = Regex.Matches(text, matching_exp);

                int firstpos = 999999999;

                foreach (Match match in matches)
                {
                    // Get the position for the first match
                    if (match.Index < firstpos)
                    {
                        firstpos = match.Index;
                    }

                    string hit_term_title = match.Groups[1].ToString();

                    //    Calculate the score of the term for each class
                    foreach (JProperty classid_obj in (JToken)term["cs"])
                    {
                        string classid = classid_obj.Name.ToString();
                        JToken term_info = term["cs"][classid];

                        int score_count = matches.Count;
                        int score_weight = score_count * Int32.Parse(term_info["w"].ToString());

                        // Keep some class information
                        if (classes[classid] == null)
                        {
                            classes[classid] = new JObject();
                        }

                        classes[classid]["title"] = term_info["c"]["t"];
                        classes[classid]["hidden"] = term_info["c"]["h"];
                        classes[classid]["exclusive"] = term_info["c"]["e"];
                        classes[classid]["superClass"] = term_info["c"]["sc"];
                        classes[classid]["thresholdWeight"] = term_info["c"]["tw"];
                        classes[classid]["thresholdCount"] = term_info["c"]["tc"];
                        classes[classid]["thresholdCountUnique"] = term_info["c"]["tcu"];
                        classes[classid]["requireClass"] = term_info["c"]["crc"];
                        classes[classid]["excludeOnClass"] = term_info["c"]["cec"];
                        classes[classid]["includeClass"] = term_info["c"]["cic"];

                        if (classes[classid]["terms"] == null)
                        {
                            classes[classid]["terms"] = new JObject();
                        }

                        if (classes[classid]["terms"][term_title] == null)
                        {
                            classes[classid]["terms"][term_title] = new JObject();
                        }

                        classes[classid]["terms"][term_title]["scoreWeight"] = score_weight;
                        classes[classid]["terms"][term_title]["scoreCount"] = score_count;
                        classes[classid]["terms"][term_title]["scoreFirstPosition"] = firstpos;

                        // Add the hit and adjust count
                        if (classes[classid]["terms"][term_title]["hits"] != null)
                        {
                            if (classes[classid]["terms"][term_title]["hits"][hit_term_title] == null)
                            {
                                classes[classid]["terms"][term_title]["hits"][hit_term_title] = 1;
                            }
                            else
                            {
                                int count = Int32.Parse(classes[classid]["terms"][term_title]["hits"][hit_term_title].ToString());

                                count++;

                                classes[classid]["terms"][term_title]["hits"][hit_term_title] = count;
                            }
                        }
                        else
                        {
                            classes[classid]["terms"][term_title]["hits"] = new JObject();

                            classes[classid]["terms"][term_title]["hits"][hit_term_title] = 1;
                        }


                        /*
                                                // A regexp with no matches the hits property is not sat
                                                if(! isset($classes[$classid]['terms'][$term_title]['hits']))
                                                {
                                                    // Remove the regexp without matches
                                                    unset($classes[$classid]['terms'][$term_title]);

                                                    continue;
                                                }
                        */
                        classes[classid]["terms"][term_title]["required"] = term_info["r"];
                        classes[classid]["terms"][term_title]["requiredOr"] = term_info["ro"];
                        classes[classid]["terms"][term_title]["requireText"] = term_info["rt"];
                        classes[classid]["terms"][term_title]["excludeOnText"] = term_info["et"];
                        classes[classid]["terms"][term_title]["requireTerm"] = term_info["rte"];
                        classes[classid]["terms"][term_title]["excludeOnTerm"] = term_info["ete"];
                        classes[classid]["terms"][term_title]["requireClass"] = term_info["rc"];
                        classes[classid]["terms"][term_title]["excludeOnClass"] = term_info["ec"];
                        classes[classid]["terms"][term_title]["forceIncludeClass"] = term_info["fic"];
                        classes[classid]["terms"][term_title]["forceExcludeClass"] = term_info["fec"];
                        classes[classid]["terms"][term_title]["forceSuperClass"] = term_info["fsc"];
                    }
                }

                //                Debug.WriteLine("t30 " + sw.ElapsedMilliseconds);
                //                sw.Restart();
            }
        }

        private static void cleanupTerms(ref JObject classes)
        {
            //	Remove terms that are substrings of other terms.
            //	
            //	This is the slow but easy way to do it.
            bool removeSubTerms = false;

            /*
                        if ($removeSubTerms == true)
                        {
                            foreach ($classes as $classid => $class)
                            {
                                foreach ($class['terms'] as $termtitle => $term)
                                {
                                    foreach ($classes as $classid2 => $class2)
                                    {
                                        foreach ($class2['terms'] as $termtitle2 => $term2)
                                        {
                                            // Make a fast and simple check
                                            if(mb_strlen($termtitle) > mb_strlen($termtitle2))
                                            {
                                                // Make the slower and accurate check
                                                if((preg_match("/^$termtitle2\W/iu", $termtitle, $matches)) || (preg_match("/\W$termtitle2\W/iu", $termtitle, $matches)) || (preg_match("/\W$termtitle2$/iu", $termtitle, $matches)))
                                                {
                                                    unset($classes[$classid2]['terms'][$termtitle2]);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
            */
        }


        private static void applyTermConstraints(ref JObject classes, JObject taxonomy_lookup, string classification_method, JObject required, JObject requiredOr, string text)
        {
            //	To catch situations where class A requires class B which requires class C
            //	and class C is missing, we perform the check the number of time that there
            //	are classes.
            int max_loops = classes.Count;

            for (int loop = 0; loop < max_loops; loop++)
            {
                List<string> remove_classids = new List<string>();
                bool class_removed = false;

                foreach (JProperty classid_obj in (JToken)classes)
                {
                    string classid = classid_obj.Name.ToString();
                    JToken tclass = classes[classid];

                    int score_count = 0;
                    JObject score_count_uniques = new JObject();
                    int score_weight = 0;

                    // We need a place to set the classification method
                    classes[classid]["classificationMethod"] = classification_method;

                    //	We can have a situation where term A is checked against term B
                    //	and then term B is removed, e.g. when requireClass for term A points to
                    //	the same class and term B requires a certain word which is not present.
                    //	
                    //	So we loop if a term is removed to allow the other terms to check constraints
                    //	again.

                    bool term_removed = true;
                    int term_max_loops = tclass["terms"].Count<JToken>();

                    for (int term_loop = 0; (term_loop < term_max_loops) && (term_removed); term_loop++)
                    {
                        List<string> remove_keys = new List<string>();

                        foreach (JProperty term_title_obj in (JToken)classes[classid]["terms"])
                        {
                            string term_title = term_title_obj.Name.ToString();
                            JToken term = classes[classid]["terms"][term_title];

                            score_count_uniques[term_title] = 1;

                            //	Force the class to be included in the result.
                            if (Int32.Parse(term["forceIncludeClass"].ToString()) == 1)
                            {
                                classes[classid]["forcedIncluded"] = term_title;
                            }

                            //	Force the class to be excluded from the result.
                            if (Int32.Parse(term["forceExcludeClass"].ToString()) == 1)
                            {
                                // Remove the class from the result
                                remove_classids.Add(classid);

                                // We removed a class so force another check
                                class_removed = true;

                                break;
                            }

                            //	Force the class to be a super class in the result.
                            if (Int32.Parse(term["forceSuperClass"].ToString()) == 1)
                            {
                                classes[classid]["forcedSuperClass"] = term_title;
                            }

                            //	For this term to be valid the required term(s) must be in the text.
                            if (term["requireText"].ToString() != "")
                            {
                                bool term_condition = true;

                                string requiretexttitle_string = term["requireText"].ToString().ToLower();

                                if (Regex.Match(requiretexttitle_string, "\\|\\||\\&\\&").Success)
                                {
                                    // We have a boolean expression
                                    // Does the exact term exists in the text? Get all occurences

                                    string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                                    string[] requiretexts = Regex.Split(requiretexttitle_string, "(\\|\\||\\&\\&)");

                                    // NOTE: The increment is 2
                                    for (int i = 0; i < requiretexts.Count<string>(); i += 2)
                                    {
                                        string requiretexttitle = requiretexts[i].ToLower();

                                        bool term_exists = false;

                                        if (requiretexttitle.Substring(0, 1) == "/")
                                        {
                                            // We got a regexp
                                            requiretexttitle = requiretexttitle.Replace("\\/", "");
                                        }

                                        string matching_exp = "(?<=[^a-z0-9" + special_characters + "\\;_\\-])(" + requiretexttitle + ")s?(?=[^a-z0-9" + special_characters + "\\&_\\-])";

                                        if (Regex.Match(text, matching_exp).Success)
                                        {
                                            term_exists = true;
                                        }

                                        if (i == 0)
                                        {
                                            term_condition = term_exists;
                                        }
                                        else
                                        {
                                            string condition = requiretexts[i - 1];

                                            term_condition = condition == "||" ? term_condition || term_exists : term_condition && term_exists;
                                        }
                                    }
                                }
                                else
                                {
                                    // We have 1 string
                                    if (Regex.Match(text, requiretexttitle_string.ToLower()).Success)
                                    {
                                        term_condition = true;
                                    }
                                    else
                                    {
                                        term_condition = false;
                                    }
                                }

                                if (term_condition == false)
                                {
                                    // We did not find the term in the text, so remove the term from the result
                                    remove_keys.Add(term_title);

                                    continue;
                                }
                            }

                            //	For this term to be valid the excluding term(s) must not be in the text.
                            if (term["excludeOnText"].ToString() != "")
                            {
                                bool term_condition = true;

                                string excludeontexttitle_string = term["excludeOnText"].ToString().ToLower();

                                if (Regex.Match(excludeontexttitle_string, "\\|\\||\\&\\&").Success)
                                {
                                    string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                                    string[] excludeontexts = Regex.Split(excludeontexttitle_string, "(\\|\\||\\&\\&)");

                                    // NOTE: The increment is 2
                                    for (int i = 0; i < excludeontexts.Count<string>(); i += 2)
                                    {
                                        string excludeontexttitle = excludeontexts[i].ToLower();

                                        bool term_exists = false;

                                        if (excludeontexttitle.Substring(0, 1) == "/")
                                        {
                                            // We got a regexp

                                            excludeontexttitle = excludeontexttitle.Replace("\\/", "");
                                        }

                                        string matching_exp = "(?<=[^a-z0-9" + special_characters + "\\;_\\-])(" + excludeontexttitle + ")s?(?=[^a-z0-9" + special_characters + "\\&_\\-])";

                                        if (Regex.Match(text, matching_exp).Success)
                                        {
                                            term_exists = true;
                                        }

                                        if (i == 0)
                                        {
                                            term_condition = term_exists;
                                        }
                                        else
                                        {
                                            string condition = excludeontexts[i - 1];

                                            term_condition = condition == "||" ? term_condition || term_exists : term_condition && term_exists;
                                        }
                                    }
                                }
                                else
                                {
                                    // We have 1 string
                                    if (Regex.Match(text, excludeontexttitle_string).Success)
                                    {
                                        term_condition = true;
                                    }
                                    else
                                    {
                                        term_condition = false;
                                    }
                                }

                                if (term_condition == true)
                                {
                                    // We did find the term in the text, so remove the term from the result
                                    remove_keys.Add(term_title);

                                    continue;
                                }
                            }

                            //	For this term to be valid the required term(s) must be in the text.
                            if (term["requireTerm"].ToString() != "")
                            {
                                bool term_condition = true;

                                string requiretermtitle_string = term["requireTerm"].ToString().ToLower();

                                if (Regex.Match(requiretermtitle_string, "\\|\\||\\&\\&").Success)
                                {
                                    string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                                    string[] requireterms = Regex.Split(requiretermtitle_string, "(\\|\\||\\&\\&)");

                                    // NOTE: The increment is 2
                                    for (int i = 0; i < requireterms.Count<string>(); i += 2)
                                    {
                                        string requiretermtitle = requireterms[i].ToLower();

                                        bool term_exists = false;

                                        if (tclass["terms"][requiretermtitle] != null)
                                        {
                                            term_exists = true;
                                        }

                                        if (i == 0)
                                        {
                                            term_condition = term_exists;
                                        }
                                        else
                                        {
                                            string condition = requireterms[i - 1];

                                            term_condition = condition == "||" ? term_condition || term_exists : term_condition && term_exists;
                                        }
                                    }
                                }
                                else
                                {
                                    // We have 1 string
                                    if (tclass["terms"][requiretermtitle_string] != null)
                                    {
                                        term_condition = true;
                                    }
                                    else
                                    {
                                        term_condition = false;
                                    }
                                }

                                if (term_condition == false)
                                {
                                    // We did not find the term in the text, so remove the term from the result
                                    remove_keys.Add(term_title);

                                    continue;
                                }
                            }

                            //	For this term to be valid the excluding term(s) must not be in the text.
                            if (term["excludeOnTerm"].ToString() != "")
                            {
                                bool term_condition = true;

                                string excludeontermtitle_string = term["excludeOnTerm"].ToString().ToLower();

                                if (Regex.Match(excludeontermtitle_string, "\\|\\||\\&\\&").Success)
                                {
                                    string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                                    string[] excludeonterms = Regex.Split(excludeontermtitle_string, "(\\|\\||\\&\\&)");

                                    // NOTE: The increment is 2
                                    for (int i = 0; i < excludeonterms.Count<string>(); i += 2)
                                    {
                                        string excludeontermtitle = excludeonterms[i].ToLower();

                                        bool term_exists = false;

                                        if (tclass["terms"][excludeontermtitle] != null)
                                        {
                                            term_exists = true;
                                        }

                                        if (i == 0)
                                        {
                                            term_condition = term_exists;
                                        }
                                        else
                                        {
                                            string condition = excludeonterms[i - 1];

                                            term_condition = condition == "||" ? term_condition || term_exists : term_condition && term_exists;
                                        }
                                    }
                                }
                                else
                                {
                                    // We have 1 string
                                    if (tclass["terms"][excludeontermtitle_string] != null)
                                    {
                                        term_condition = true;
                                    }
                                    else
                                    {
                                        term_condition = false;
                                    }
                                }

                                if (term_condition == true)
                                {
                                    // We did not find the term in the text, so remove the term from the result
                                    remove_keys.Add(term_title);

                                    continue;
                                }
                            }

                            //	For this term to be valid the required class must be in the list.
                            if (term["requireClass"].ToString() != "")
                            {
                                bool term_condition = true;

                                string requiredclassid_string = term["requireClass"].ToString();

                                if (requiredclassid_string[0] == '^')
                                {
                                    Debug.WriteLine("1001 " + classes);
                                }
                                else
                                {
                                    if (Regex.Match(requiredclassid_string, "\\|\\||\\&\\&").Success)
                                    {
                                        string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                                        string[] requiredclassids = Regex.Split(requiredclassid_string, "(\\|\\||\\&\\&)");

                                        // NOTE: The increment is 2
                                        for (int i = 0; i < requiredclassids.Count<string>(); i += 2)
                                        {
                                            string requiredclassid = requiredclassids[i].ToLower();

                                            bool id_exists = true;

                                            if (classes[requiredclassid] == null)
                                            {
                                                id_exists = false;
                                            }
                                            else
                                            {
                                                //	It is possible for a term to require the class it lives in, so we need to check that at least one other term within the class was hit
                                                if (requiredclassid == classid)
                                                {
                                                    bool found_another_term = false;

                                                    foreach (JProperty terms_term_title_obj in (JToken)classes[classid]["terms"])
                                                    {
                                                        string terms_term_title = terms_term_title_obj.Name.ToString();

                                                        if (term_title != terms_term_title)
                                                        {
                                                            found_another_term = true;

                                                            break;
                                                        }
                                                    }

                                                    if (found_another_term == false)
                                                    {
                                                        //	We did not find another term in the class, so remove this term. 
                                                        //	Because this was the only term in the class the class will be removed.

                                                        id_exists = false;
                                                    }
                                                }
                                            }

                                            if (i == 0)
                                            {
                                                term_condition = id_exists;
                                            }
                                            else
                                            {
                                                string condition = requiredclassids[i - 1];

                                                term_condition = condition == "||" ? term_condition || id_exists : term_condition && id_exists;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // We have 1 string
                                        term_condition = false;

                                        if (classes[requiredclassid_string] != null)
                                        {
                                            if (classid == requiredclassid_string)
                                            {
                                                // Requiring own class means at least 2 different terms must exists
                                                if (classes[classid]["terms"].Count<JToken>() > 1)
                                                {
                                                    term_condition = true;
                                                }
                                            }
                                            else
                                            {
                                                // The required class exists
                                                term_condition = true;
                                            }
                                        }
                                    }
                                }

                                if (term_condition == false)
                                {
                                    // We did not find the term in the text, so remove the term from the result
                                    remove_keys.Add(term_title);

                                    continue;
                                }
                            }

                            //	For this term to be valid the excluding class must not be in the list.
                            if (term["excludeOnClass"].ToString() != "")
                            {
                                bool term_condition = true;

                                string excludeonclassid_string = term["excludeOnClass"].ToString();

                                if (Regex.Match(excludeonclassid_string, "\\|\\||\\&\\&").Success)
                                {
                                    string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                                    string[] excludeonclassids = Regex.Split(excludeonclassid_string, "(\\|\\||\\&\\&)");

                                    // NOTE: The increment is 2
                                    for (int i = 0; i < excludeonclassids.Count<string>(); i += 2)
                                    {
                                        string excludeonclassid = excludeonclassids[i].ToLower();

                                        bool id_exists = true;

                                        if (classes[excludeonclassid] == null)
                                        {
                                            id_exists = false;
                                        }

                                        if (i == 0)
                                        {
                                            term_condition = id_exists;
                                        }
                                        else
                                        {
                                            string condition = excludeonclassids[i - 1];

                                            term_condition = condition == "||" ? term_condition || id_exists : term_condition && id_exists;
                                        }
                                    }
                                }
                                else
                                {
                                    // We have 1 string
                                    if (classes[excludeonclassid_string] != null)
                                    {
                                        term_condition = true;
                                    }
                                    else
                                    {
                                        term_condition = false;
                                    }
                                }

                                if (term_condition == true)
                                {
                                    // We did not find the term in the text, so remove the term from the result
                                    remove_keys.Add(term_title);

                                    continue;
                                }
                            }

                            score_weight += Int32.Parse(term["scoreWeight"].ToString());
                            score_count += Int32.Parse(term["scoreCount"].ToString());
                        }

                        // Actual remove the removed terms
                        foreach (string key in remove_keys)
                        {
                            classes[classid]["terms"][key].Parent.Remove();

                            term_removed = true;
                        }

                        if (term_removed == false)
                        {
                            // No terms were removed to stop looping
                            break;
                        }
                    }

                    bool classForcedIncluded = false;

                    //	Check whether the class was forced to super
                    if (classes[classid]["forcedSuperClass"] != null)
                    {
                        classForcedIncluded = true;

                        classes[classid]["superClass"] = 1;

                        // Remove this temporary setting
                        classes[classid]["forcedSuperClass"].Parent.Remove();
                    }

                    //	Check whether the class was forced included
                    if (classes[classid]["forcedIncluded"] != null)
                    {
                        classForcedIncluded = true;

                        // Remove this temporary setting
                        classes[classid]["forcedIncluded"].Parent.Remove();
                    }

                    //	If the class wasn't forced to be included check 
                    //	whether it meets all requirements.
                    if (classForcedIncluded == false)
                    {
                        //	Check whether the required term(s) are present.
                        if ((required[classid] != null) && (class_removed == false))
                        {
                            foreach (JProperty term_title_obj in (JToken)required[classid])
                            {
                                string term_title = term_title_obj.Name.ToString();
                                JToken term = classes[classid]["terms"][term_title];

                                if (classes[classid]["terms"][term_title] == null)
                                {
                                    // A required term is missing, so remove class
                                    remove_classids.Add(classid);

                                    // We removed a class so force another check
                                    class_removed = true;
                                }
                            }
                        }

                        //	Check that at least one requiredOr term(s) are present.
                        if ((requiredOr[classid] != null) && (class_removed == false))
                        {
                            bool found_one = false;

                            foreach (JProperty term_title_obj in (JToken)requiredOr[classid])
                            {
                                string term_title = term_title_obj.Name.ToString();
                                JToken term = classes[classid]["terms"][term_title];

                                if (classes[classid]["terms"][term_title] != null)
                                {
                                    // We found at least one requiredOr term
                                    found_one = true;

                                    break;
                                }
                            }

                            if (found_one == false)
                            {
                                remove_classids.Add(classid);

                                // We removed a class so force another check
                                class_removed = true;
                            }
                        }

                        //	Check whether the terms in the class score high enough
                        //                        if (((score_count < Int32.Parse(tclass["thresholdCount"].ToString())) || (score_weight < Int32.Parse(tclass["thresholdWeight"].ToString()))) && (class_removed == false))
                        if ((score_count < Int32.Parse(tclass["thresholdCount"].ToString())) || (score_weight < Int32.Parse(tclass["thresholdWeight"].ToString())))
                        {
                            remove_classids.Add(classid);

                            // We removed a class so force another check
                            class_removed = true;

                            // Skip to the next class id
                            continue;
                        }

                        //	Check whether the unique terms in the class score high enough
                        int score_count_unique = score_count_uniques.Count;

                        //                        if ((score_count_unique < Int32.Parse(tclass["thresholdCountUnique"].ToString())) && (class_removed == false))
                        if ((score_count_unique < Int32.Parse(tclass["thresholdCountUnique"].ToString())))
                        {
                            remove_classids.Add(classid);

                            // We removed a class so force another check
                            class_removed = true;

                            // Skip to the next class id
                            continue;
                        }
                        /*
                                                //	Check whether the terms with the required flag set are present
                                                if(required_terms[classid] != null))
                                                {
                                                    foreach($required_terms[$classid] as $term_title => $term)
                                                    {
                                                        if( ! in_array($term_title, array_keys($classes[$classid]['terms'])))
                                                        {
                                                            unset($classes[$classid]);

                                                            // We removed a class so force another check				
                                                            $class_removed = 1;
                                                        }
                                                    }
                                                }
                        */
                    }
                }

                foreach (string removed_classid in remove_classids)
                {
                    if (classes[removed_classid] != null)
                    {
                        classes[removed_classid].Parent.Remove();
                    }
                }
            }
        }

        private static void cleanupClasses(ref JObject classes)
        {
            List<string> remove_keys = new List<string>();

            //	Remove any empty classes.
            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();
                JToken tclass = classes[classid];

                if (tclass["terms"].Count<JToken>() == 0)
                {
                    remove_keys.Add(classid);
                }
            }

            // Actual remove the removed classes
            foreach (string key in remove_keys)
            {
                classes.Remove(key);
            }
        }


        private static void applyClassConstraints(ref JObject classes, string text)
        {
            //	Check whether the class is exclusive.
            //	
            //	Exclusive means that the class does not have
            //	any siblings at the same level.

            List<string> remove_keys = new List<string>();

            //	Check whether the class is exclusive
            //  exclusive means that all siblings are removed. 
            //  At the Top level this means all other classes
            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();
                JToken tclass = classes[classid];

                if (Int32.Parse(tclass["exclusive"].ToString()) == 1)
                {
                    // Handle top level classes separately
                    if (Regex.Match(classid, "^[0-9]+$").Success)
                    {
                        foreach (JProperty cid_obj in (JToken)classes)
                        {
                            string cid = cid_obj.Name.ToString();

                            if ((cid != classid) && (Regex.Match(cid, "^[0-9]+$").Success))
                            {
                                //	Remove class
                                remove_keys.Add(cid);

                                //                                break;
                            }
                        }
                    }
                    else
                    {
                        string parent_classid = classid.Replace("\\.[0-9]+$", "");

                        foreach (JProperty cid_obj in (JToken)classes)
                        {
                            string cid = cid_obj.Name.ToString();

                            string parent_cid = cid.Replace("\\.[0-9]+$", "");

                            if ((cid != classid) && (parent_cid == parent_classid))
                            {
                                //	Another sibling class was found, so remove this class
                                remove_keys.Add(cid);

                                //                                break;
                            }
                        }
                    }
                }
            }

            foreach (string removed_classid in remove_keys)
            {
                classes[removed_classid].Parent.Remove();
            }

            remove_keys.Clear();

            //	Check whether there are any super classes and remove those who are not
            bool superClassFound = false;

            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();
                JToken tclass = classes[classid];

                if (Int32.Parse(tclass["superClass"].ToString()) == 1)
                {
                    //	We found a super class
                    superClassFound = true;

                    break;
                }
            }

            if (superClassFound)
            {
                foreach (JProperty classid_obj in (JToken)classes)
                {
                    string classid = classid_obj.Name.ToString();
                    JToken tclass = classes[classid];

                    if (Int32.Parse(tclass["superClass"].ToString()) != 1)
                    {
                        //	The class is not a super class so remove it
                        remove_keys.Add(classid);
                    }
                }
            }

            foreach (string removed_classid in remove_keys)
            {
                classes[removed_classid].Parent.Remove();
            }

            remove_keys.Clear();

            //	Check whether the class requires classes
            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();
                JToken tclass = classes[classid];

                if (tclass["requireClass"].ToString() != "")
                {
                    bool class_condition = true;

                    //	The class requires classes
                    string requireclass_string = tclass["requireClass"].ToString();
                    if (classes[requireclass_string.Substring(1)] != null)
                    {
                        bool foundTermsInConnection = false;

                        if (requireclass_string[0] == '^')
                        {
                            foreach (JProperty termname in (JToken)tclass["terms"])
                            {
                                foreach (JProperty hitname in (JToken)tclass["terms"][termname.Name.ToString()]["hits"])
                                {
                                    string hit_str = hitname.Name.ToString();

                                    // Note the spaces around text
                                    MatchCollection matches = Regex.Matches(" " + text + " ", "[^\\.\\!\\?]{0,50} " + hit_str + "[^a-z" + special_characters + "][^\\.\\!\\?]{0,30}");

                                    foreach (Match match in matches)
                                    {
                                        foreach (JProperty requiredClassTerm in (JToken)classes[requireclass_string.Substring(1)]["terms"])
                                        {
                                            foreach (JProperty requiredClassTermHit in (JToken)classes[requireclass_string.Substring(1)]["terms"][requiredClassTerm.Name.ToString()]["hits"])
                                            {
                                                string required_hit_str = requiredClassTermHit.Name.ToString();

                                                if (Regex.IsMatch(match.ToString(), required_hit_str) == true)
                                                {
                                                    foundTermsInConnection = true;

                                                    // Yes, a bona fide goto
                                                    goto break_out_label;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    // Yes, a bona fide goto label
                    break_out_label:

                        class_condition = foundTermsInConnection;
                    }
                    else
                    {
                        // TODO: BUG: Only | is accepted at present
                        if (Regex.Match(requireclass_string, "\\|\\||\\&\\&").Success)
                        {
                            string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                            string[] requireclassids = Regex.Split(requireclass_string, "(\\|\\||\\&\\&)");

                            // NOTE: The increment is 2
                            for (int i = 0; i < requireclassids.Count<string>(); i += 2)
                            {
                                string requireclass = requireclassids[i];

                                bool class_exists = false;

                                if (classes[requireclass] != null)
                                {
                                    class_exists = true;
                                }

                                if (i == 0)
                                {
                                    class_condition = class_exists;
                                }
                                else
                                {
                                    string condition = requireclassids[i - 1];

                                    class_condition = condition == "||" ? class_condition || class_exists : class_condition && class_exists;
                                }
                            }
                        }
                        else
                        {
                            // We have 1 string
                            if (classes[requireclass_string] != null)
                            {
                                //                            Debug.WriteLine("1450 " + classes);

                                class_condition = true;
                            }
                            else
                            {
                                class_condition = false;
                            }
                        }
                    }

                    if (class_condition == false)
                    {
                        // We did not find the required class in the result, so remove this class from the result
                        remove_keys.Add(classid);

                        continue;
                    }
                }
            }

            foreach (string removed_classid in remove_keys)
            {
                classes[removed_classid].Parent.Remove();
            }

            remove_keys.Clear();

            //	Check whether the class should be excluded on classes
            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();
                JToken tclass = classes[classid];

                if (tclass["excludeOnClass"].ToString() != "")
                {
                    bool class_condition = true;

                    //	The class requires classes
                    string excludeonclass_string = tclass["excludeOnClass"].ToString();

                    // TODO: BUG: Only | is accepted at present
                    if (Regex.Match(excludeonclass_string, "\\|\\||\\&\\&").Success)
                    {
                        string[] separators = new string[] { "\\|\\|", "\\&\\&" };
                        string[] excludeoneclassids = Regex.Split(excludeonclass_string, "(\\|\\||\\&\\&)");

                        // NOTE: The increment is 2
                        for (int i = 0; i < excludeoneclassids.Count<string>(); i += 2)
                        {
                            string excludeonclass = excludeoneclassids[i];

                            bool class_exists = false;

                            if (classes[excludeonclass] != null)
                            {
                                class_exists = true;
                            }

                            if (i == 0)
                            {
                                class_condition = class_exists;
                            }
                            else
                            {
                                string condition = excludeoneclassids[i - 1];

                                class_condition = condition == "||" ? class_condition || class_exists : class_condition && class_exists;
                            }
                        }
                    }
                    else
                    {
                        // We have 1 string
                        if (classes[excludeonclass_string] != null)
                        {
                            class_condition = true;
                        }
                        else
                        {
                            class_condition = false;
                        }
                    }

                    if (class_condition == true)
                    {
                        // We did find the required class in the result, so remove this class from the result
                        remove_keys.Add(classid);

                        continue;
                    }
                }
            }

            foreach (string removed_classid in remove_keys)
            {
                classes[removed_classid].Parent.Remove();
            }

            remove_keys.Clear();

            //	Check whether the class is hidden.
            //	This must be after the checks for requireClass and excludeOnClass
            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();
                JToken tclass = classes[classid];

                if (Int32.Parse(tclass["hidden"].ToString()) == 1)
                {
                    //	The class is not a super class so remove it
                    remove_keys.Add(classid);
                }
            }

            foreach (string removed_classid in remove_keys)
            {
                classes[removed_classid].Parent.Remove();
            }

            remove_keys.Clear();
        }

        public static void calculateScores(ref JObject classes, int firstClassExtraWeight, string type)
        {
            switch (type)
            {
                case "standard":
                    calculateScoresStandard(ref classes, firstClassExtraWeight);
                    break;

                default:
                    calculateScoresStandard(ref classes, firstClassExtraWeight);
                    break;
            }
        }

        static void calculateScoresStandard(ref JObject classes, int firstClassExtraWeight)
        {
            /*
                Calculate the scores for each class.

                We get:
                    a weight score, 
                    a count score, 
                    a position score and 
                    a 'first class' score (is the class the first found in the text or not).
                    a total score

            */

            string last_classid = "";
            string first_position_classid = "";
            int last_position = 999999999;

            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();
                JToken tclass = classes[classid];

                int score_count = 0;
                int score_weight = 0;
                int score_position = 999999999;
                bool score_first = false;

                foreach (JProperty termtitle_obj in (JToken)tclass["terms"])
                {
                    string termtitle = termtitle_obj.Name.ToString();
                    JToken term_info = tclass["terms"][termtitle];

                    score_count += Int32.Parse(term_info["scoreCount"].ToString());
                    score_weight += Int32.Parse(term_info["scoreWeight"].ToString());

                    if (score_position > Int32.Parse(term_info["scoreFirstPosition"].ToString()))
                    {
                        score_position = Int32.Parse(term_info["scoreFirstPosition"].ToString());
                    }
                }

                classes[classid]["scoreCount"] = score_count;
                classes[classid]["scoreWeight"] = score_weight;
                classes[classid]["scoreFirstPosition"] = score_position;
                classes[classid]["scoreFirstPositionExtraWeight"] = 0;

                // Termine whether the class includes the first found term.
                // If so, reward the class with som extra weight.

                Debug.WriteLine("1758 " + classid + "," + firstClassExtraWeight + " :: " + last_position + " > " + score_position);
                Debug.WriteLine("1759 " + score_count + "," + score_weight);

                if (last_position > score_position)
                {
                    if (last_classid != "")
                    {
                        // There might be more classes with the first position score
                        foreach (JProperty cid_obj in (JToken)classes)
                        {
                            string cid = cid_obj.Name.ToString();
                            //                            JToken tclass = classes[classid];

                            classes[cid]["scoreFirstPositionExtraWeight"] = 0;
                        }
                    }

                    classes[classid]["scoreFirstPositionExtraWeight"] = firstClassExtraWeight;

                    last_position = score_position;
                    last_classid = classid;
                    first_position_classid = classid;
                }
                else
                {
                    if (last_position == score_position)
                    {
                        classes[classid]["scoreFirstPositionExtraWeight"] = firstClassExtraWeight;

                        last_position = score_position;
                        last_classid = classid;
                        first_position_classid = classid;
                    }
                }

                // Get the total score
                classes[classid]["scoreTotal"] = classes[classid]["scoreWeight"];
            }

            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();

                // Update score
                int score = Int32.Parse(classes[classid]["scoreTotal"].ToString());
                score += Int32.Parse(classes[classid]["scoreFirstPositionExtraWeight"].ToString());
                classes[classid]["scoreTotal"] = score;
            }
        }

        public static void sortClassesByWeight(ref JObject classes)
        {
            SortedList<int, List<string>> classesByWeight = new SortedList<int, List<string>>();

            foreach (JProperty classid_obj in (JToken)classes)
            {
                string classid = classid_obj.Name.ToString();

                int scoreTotal = Int32.Parse(classes[classid]["scoreTotal"].ToString());

                if (!classesByWeight.ContainsKey(scoreTotal))
                {
                    classesByWeight[scoreTotal] = new List<string>();
                }

                classesByWeight[scoreTotal].Add(classid);
            }

            JObject sortedClasses = new JObject();

            foreach (int key in classesByWeight.Keys.Reverse<int>())
            {
                foreach (string classid in classesByWeight[key])
                {
                    sortedClasses[classid] = classes[classid];
                }
            }

            classes = sortedClasses;
        }

        public static void calculateConfidenceCoefficient(ref JObject classes, string type)
        {
            switch (type)
            {
                case "standard":
                    calculateConfidenceCoefficientstandard(ref classes);
                    break;

                default:
                    calculateConfidenceCoefficientstandard(ref classes);
                    break;
            }
        }

        private static void calculateConfidenceCoefficientstandard(ref JObject classes)
        {
            //  The confidence coefficient is the percentage that the 
            //    weight difference between the first and second divided
            //    by the totale weight of all classes.

            //    Note that $classes is passed by reference, so operations must
            //    directly on $classes.

            // With 1 class we are 100% certain that this is the class
            if (classes.Count == 1)
            {
                string classid = classes.Properties().Select(p => p.Name).FirstOrDefault();

                classes[classid]["scoreConfidenceCoefficient"] = 100;
            }

            if (classes.Count > 1)
            {
                string firstClassid = "";
                int firstClassScore = 0;
                int secondClassScore = 0;

                foreach (JProperty classid_obj in (JToken)classes)
                {
                    string classid = classid_obj.Name.ToString();
                    JToken tclass = classes[classid];

                    if (firstClassid == "")
                    {
                        firstClassid = classid;
                        firstClassScore = Int32.Parse(classes[classid]["scoreTotal"].ToString());

                        continue;
                    }

                    secondClassScore = Int32.Parse(classes[classid]["scoreTotal"].ToString());

                    // We gor the first and the second score so stop looping
                    break;
                }

                // Must be double
                double weight_difference = firstClassScore - secondClassScore;

                classes[firstClassid]["scoreConfidenceCoefficient"] = (int)Math.Floor((weight_difference / firstClassScore) * 100);
            }
        }
    }
}
