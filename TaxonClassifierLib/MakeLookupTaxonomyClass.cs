using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace TaxonClassifierLib
{
    internal class MakeLookupTaxonomyClass
    {
        public static string makeLookupTaxonomy(string json)
        {
            if (json == "")
            {
                return "{}";
            }

            JObject taxonomy = new JObject();

            try
            {
                taxonomy = JObject.Parse(json);
            }
            catch (Exception e)
            {
                Debug.WriteLine("CATCH lookup 22: " + e.Message);

                return "{}";
            }

            JObject lookupTaxonomy = new JObject();

            JObject terms_lookup = new JObject();

            JObject errors = new JObject();

            // At the moment there is only 'compact'
            string mode = "compact";

            int class_number = 0;

            string settings_json = @"{
                ""compact"": {
                    ""classes"": ""cs"",
                    ""class"": {
                        ""title"": ""t"",
                        ""id"": ""i"",
                        ""hidden"": ""h"",
                        ""exclusive"": ""e"",
                        ""superClass"": ""sc"",
                        ""thresholdWeight"": ""tw"",
                        ""thresholdCount"": ""tc"",
                        ""thresholdCountUnique"": ""tcu"",
                        ""requireClass"": ""crc"",
                        ""excludeOnClass"": ""cec"",
                        ""includeClass"": ""cic"",
                    },
                    ""term"": {
                        ""title"": ""t"",
                        ""weight"": ""w"",
                        ""required"": ""r"",
                        ""requiredOr"": ""ro"",
                        ""requireText"": ""rt"",
                        ""excludeOnText"": ""et"",
                        ""requireTerm"": ""rte"",
                        ""excludeOnTerm"": ""ete"",
                        ""requireClass"": ""rc"",
                        ""excludeOnClass"": ""ec"",
                        ""prefix"": ""p"",
                        ""suffix"": ""s"",
                        ""forceIncludeClass"": ""fic"",
                        ""forceExcludeClass"": ""fec"",
                        ""forceSuperClass"": ""fsc"",
                        ""class"": ""c""
                    }
                }
            }";

            JObject setting_names = JObject.Parse(settings_json);

            foreach (JProperty tclass in taxonomy["classes"])
            {
                class_number++;

                string classid = tclass.Name.ToString();

                // Check whether the title of the class is defined	
                if (taxonomy["classes"][classid]["title"] == null)
                {
                    if (taxonomy["classes"][classid]["id"] == null)
                    {
                        errors["X" + class_number.ToString()] = "Title is not defined\n";
                    }
                    else
                    {
                        errors[classid] = "Title is not defined\n";
                    }

                    continue;
                }

                // Check whether the id of the class is defined	
                if (taxonomy["classes"][classid]["id"] == null)
                {
                    errors[classid] = "ID is not defined in class " + taxonomy["classes"][classid]["title"] + "\n";

                    continue;
                }

                if (taxonomy["classes"][classid]["terms"] != null)
                {
                    if (taxonomy["classes"][classid]["includeClass"].ToString() != "")
                    {
                        string includeClass_string = taxonomy["classes"][classid]["includeClass"].ToString();

                        // TODO: Handle including multiple class. 
                        // Below is the original PHP code
                        /*				
                                                includeClasses = preg_split("/\|/u", $includeClass_string, -1, PREG_SPLIT_DELIM_CAPTURE);

                                                foreach($includeClasses as $includeClass)
                                                {
                                                    foreach($taxonomy['classes'][$includeClass]['terms'] as $term_title => $term)
                                                    {

                                                        if(! isset($class['terms'] [$term_title]))
                                                        {
                                                            // If the included term requires it's own class ID change 
                                                            // the requireClass to the including class ID
                                                            if($term['requireClass'] == $includeClass)
                                                            {
                                                                $term['requireClass'] = $class['id'];
                                                            }

                                                            // If the included term excludes on it's own class ID change 
                                                            // the excludeOnClass to the including class ID
                                                            if($term['excludeOnClass'] == $includeClass)
                                                            {
                                                                $term['excludeOnClass'] = $class['id'];
                                                            }

                                                            $class['terms'] [] = $term;
                                                        }
                                                    }
                                                }
                        */
                    }

                    string short_classes = setting_names[mode]["classes"].ToString();
                    string short_class = setting_names[mode]["term"]["class"].ToString();

                    foreach (JProperty term in (JToken)taxonomy["classes"][classid]["terms"])
                    {
                        // Make the title lower case for later comparison in TaxonClassifier
                        string termTitle = term.Value["title"].ToString();
                        string termTitle_lc = termTitle.ToLower();

                        if (terms_lookup[termTitle_lc] == null)
                        {
                            terms_lookup[termTitle_lc] = new JObject();
                        }

                        if (terms_lookup[termTitle_lc][short_classes.ToString()] == null)
                        {
                            terms_lookup[termTitle_lc][short_classes.ToString()] = new JObject();
                        }

                        if (terms_lookup[termTitle_lc][short_classes.ToString()][classid] == null)
                        {
                            terms_lookup[termTitle_lc][short_classes.ToString()][classid] = new JObject();
                        }

                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["weight"].ToString()] = term.Value["weight"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["required"].ToString()] = term.Value["required"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["requiredOr"].ToString()] = term.Value["requiredOr"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["requireText"].ToString()] = term.Value["requireText"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["excludeOnText"].ToString()] = term.Value["excludeOnText"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["requireTerm"].ToString()] = term.Value["requireTerm"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["excludeOnTerm"].ToString()] = term.Value["excludeOnTerm"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["requireClass"].ToString()] = term.Value["requireClass"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["excludeOnClass"].ToString()] = term.Value["excludeOnClass"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["forceIncludeClass"].ToString()] = term.Value["forceIncludeClass"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["forceExcludeClass"].ToString()] = term.Value["forceExcludeClass"];
                        terms_lookup[termTitle_lc][short_classes][classid][setting_names[mode]["term"]["forceSuperClass"].ToString()] = term.Value["forceSuperClass"];

                        // Merge prefixes
                        if (terms_lookup[termTitle_lc][setting_names[mode]["term"]["prefix"].ToString()] == null)
                        {
                            terms_lookup[termTitle_lc][setting_names[mode]["term"]["prefix"].ToString()] = term.Value["prefix"];
                        }
                        else
                        {
                            if (terms_lookup[termTitle_lc][setting_names[mode]["term"]["prefix"].ToString()].ToString() == "")
                            {
                                terms_lookup[termTitle_lc][setting_names[mode]["term"]["prefix"].ToString()] = term.Value["prefix"];
                            }
                            else
                            {
                                if (term.Value["prefix"].ToString() != "")
                                {
                                    terms_lookup[termTitle_lc][setting_names[mode]["term"]["prefix"].ToString()] += "|" + term.Value["prefix"].ToString();
                                }
                            }
                        }

                        // Merge suffixes
                        if (terms_lookup[termTitle_lc][setting_names[mode]["term"]["suffix"].ToString()] == null)
                        {
                            terms_lookup[termTitle_lc][setting_names[mode]["term"]["suffix"].ToString()] = term.Value["suffix"];
                        }
                        else
                        {
                            if (terms_lookup[termTitle_lc][setting_names[mode]["term"]["suffix"].ToString()].ToString() == "")
                            {
                                terms_lookup[termTitle_lc][setting_names[mode]["term"]["suffix"].ToString()] = term.Value["suffix"];
                            }
                            else
                            {
                                if (term.Value["suffix"].ToString() != "")
                                {
                                    // Some old PHP code to handle the suffixes when a term is in more than 1 class. The suffix may be different in the 2 classes.
                                    /*
                                                                        // Ensure the minimal suffix
                                                                        suffix_old = explode("|", $terms_lookup[$term['title']][$setting_names[$mode]['term']['suffix']]);
                                                                        suffix_new = explode("|", $term['suffix']);

                                                                        // NOTE: A lot of things are going on here
                                                                        $suffix = implode("|", array_keys(array_flip(array_merge($suffix_old, $suffix_new))));

                                                                        $terms_lookup[$term['title']][$setting_names[$mode]['term']['suffix']] = $suffix;
                                    */
                                }
                            }
                        }

                        if (terms_lookup[termTitle_lc][short_classes][classid][short_class] == null)
                        {
                            terms_lookup[termTitle_lc][short_classes][classid][short_class] = new JObject();
                        }

                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["title"].ToString()] = tclass.Value["title"];
                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["hidden"].ToString()] = tclass.Value["hidden"];
                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["exclusive"].ToString()] = tclass.Value["exclusive"];
                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["superClass"].ToString()] = tclass.Value["superClass"];
                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["thresholdWeight"].ToString()] = tclass.Value["thresholdWeight"];
                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["thresholdCount"].ToString()] = tclass.Value["thresholdCount"];
                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["thresholdCountUnique"].ToString()] = tclass.Value["thresholdCountUnique"];

                        // rcss = require class same sentence
                        // meaning that a term from the required class must be within 50 char to the left or to the right of the terms position, ie. same sentence
                        if (tclass.Value["comment"].ToString() == "rcss")
                        {
                            terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["requireClass"].ToString()] = "^" + tclass.Value["requireClass"];
                        }
                        else
                        {
                            terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["requireClass"].ToString()] = tclass.Value["requireClass"];
                        }

                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["excludeOnClass"].ToString()] = tclass.Value["excludeOnClass"];
                        terms_lookup[termTitle_lc][short_classes][classid][short_class][setting_names[mode]["class"]["includeClass"].ToString()] = tclass.Value["includeClass"];
                    }
                }
            }

            lookupTaxonomy["system"] = taxonomy["system"];
            lookupTaxonomy["classes"] = terms_lookup;

            // Return the lookup taxonomy as JSON and as compact as possible
            string json_lookup = lookupTaxonomy.ToString(Newtonsoft.Json.Formatting.None);

            json_lookup = Regex.Replace(json_lookup, "},", "},\n");

            return json_lookup;
        }

        public static string makeMapping(string json)
        {
            JObject mapping = new JObject();
            string json_mapping = "";

            try
            {
                JObject taxonomy = JObject.Parse(json);

                foreach (JProperty class_obj in (JToken)taxonomy["classes"])
                {
                    string classid = class_obj.Name.ToString();
                    string title = taxonomy["classes"][classid]["title"].ToString();

                    mapping[classid] = title;
                }

                json_mapping = mapping.ToString();

                json_mapping = Regex.Replace(json_mapping, "},", "},\n");
            }
            catch (Exception e)
            {
                Debug.WriteLine("CATCH lookup 269: " + e.Message + "\n" + json);
            }

            return json_mapping;
        }
    }
}
