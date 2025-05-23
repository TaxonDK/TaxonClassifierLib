using Newtonsoft.Json.Linq;

namespace TaxonClassifierLib
{
    public class ClassifyClass
    {
        public static string special_characters = "ẅëẗÿüïöäḧẍẃéŕýúíóṕǻáśǵḱĺǽǿźćǘńḿẁèỳùìòàùǹåæøẽỹũĩõãṽñŵêŷûîôâŝĝĥĵẑĉþœßŋħĸłµçð";

        public static JObject classify(string text, JObject taxonomy, JObject settings)
        {
            JObject result = new JObject();
            JObject classes = new JObject();

            // Some new code

            return result;
        }
    }
}
