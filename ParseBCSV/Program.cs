using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ParseBCSV
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("ParseBCSV csv|json filename");
                Console.WriteLine("No input specified");
                return;
            }

            if (args[0] != "json" && args[0] != "csv")
            {
                Console.WriteLine("ParseBCSV csv|json filename");
                Console.WriteLine("No output type specified");
                return;
            }

            if (!File.Exists(args[1]))
            {
                Console.WriteLine("Input file does not exist");
                return;
            }

            var path = Path.GetDirectoryName(args[1]);
            var file = Path.GetFileNameWithoutExtension(args[1]);

            try
            {
                var f = File.OpenRead(args[1]);
                var bcsvFile = new BCSVParse();
                bcsvFile.Read(new BinaryReader(f));

                switch (args[0])
                {
                    case "json":
                    {
                        var json = ConvertToJSON(bcsvFile);
                        File.WriteAllText($@"{path}\{file}.json", json);
                        break;
                    }
                    case "csv":
                    {
                        var csv = ConvertToCSV(bcsvFile);
                        File.WriteAllText($@"{path}\{file}.csv", csv);
                        break;
                    }
                }

                Console.WriteLine("OK!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static string ConvertToJSON(BCSVParse bcsv)
        {
          return JsonConvert.SerializeObject(bcsv.Entries.Select(c => c.Fields), Formatting.Indented);
        }

        private static string ConvertToCSV(BCSVParse bcsv, bool hexHeader = false)
        {
            using var textWriter = new StringWriter();

            var fields = bcsv.Entries.FirstOrDefault()?.Fields;
            if (fields != null)
            {
                var headers = fields.Keys.Select(s => s.Split(' ')[0].Replace(".HashRef", ""));
                textWriter.WriteLine( $"{string.Join(",", hexHeader ? fields.Keys : headers)}");
            }

            foreach (var t in bcsv.Entries)
            {
                textWriter.WriteLine($"{string.Join(",", t.Fields.Values)}");
            }

            return textWriter.ToString();
        }
    }
}
