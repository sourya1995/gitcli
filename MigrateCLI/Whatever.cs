using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ConsoleApp1
{
    class Whatever
    {
        static void Main(string[] args)
        {
            try
            {
                var flowGenerator = new NetworkFlowGenerator();
                flowGenerator.GenerateNetworkFlow();
            }

            catch (IOException ex)
            {
                Console.WriteLine($"File system error occurred: {ex.Message}");
                Console.WriteLine("This might be due to:");
                Console.WriteLine("- Insufficient permissions to write to the directory");
                Console.WriteLine("- File being used by another process");
                Console.WriteLine("- Disk space issues");
                Console.WriteLine("\nPlease ensure you have write permissions and sufficient disk space.");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Invalid input provided: {ex.Message}");
                Console.WriteLine("Please ensure all inputs are in the correct format.");
            }
            catch (OutOfMemoryException ex)
            {
                Console.WriteLine($"Memory error: {ex.Message}");
                Console.WriteLine("The program ran out of memory. This might happen if:");
                Console.WriteLine("- Too many zones or rules are being processed");
                Console.WriteLine("- System is low on available memory");
                Console.WriteLine("\nTry closing other applications or reducing the data size.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                Console.WriteLine($"Error type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Additional details: {ex.InnerException.Message}");
                }
                Console.WriteLine("\nStack trace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }

            finally
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }

    public class NetworkFlowGenerator
    {
        private string swimlaneName;
        private Dictionary<string, ZoneConfiguration> zones = new Dictionary<string, ZoneConfiguration>();

        public void GenerateNetworkFlow()
        {
            Console.WriteLine("\nEnter swimlane name:");
            swimlaneName = GetRequiredInput();

            ProcessZones();
            SaveToYaml();

  
        }

        private void ProcessZones()
        {
            bool continueAddingZones = true;
            while (continueAddingZones)
            {
                Console.WriteLine("\nEnter zone name:");
                string zoneName = GetRequiredInput();

                var zoneConfig = new ZoneConfiguration();

                // Ask if user wants to add CidrIpIngressRules
                if (GetYesNoResponse("Do you want to add CIDR IP ingress rules?"))
                {
                    ProcessCidrIpRules(zoneConfig, true);
                }

                // Ask if user wants to add CidrIpEgressRules
                if (GetYesNoResponse("Do you want to add CIDR IP egress rules?"))
                {
                    ProcessCidrIpRules(zoneConfig, false);
                }

                // Ask if user wants to add SwimlaneIngressRules
                if (GetYesNoResponse("Do you want to add swimlane ingress rules?"))
                {
                    ProcessSwimlaneRules(zoneConfig, true);
                }

                // Ask if user wants to add SwimlaneEgressRules
                if (GetYesNoResponse("Do you want to add swimlane egress rules?"))
                {
                    ProcessSwimlaneRules(zoneConfig, false);
                }

                zones[zoneName] = zoneConfig;
                continueAddingZones = GetYesNoResponse("Do you want to add another zone?");
            }
        }

        private void ProcessCidrIpRules(ZoneConfiguration zoneConfig, bool isIngress)
        {
            var rules = new List<CidrIpRule>();
            bool continueAddingRules = true;

            while (continueAddingRules)
            {
                Console.WriteLine($"\nAdding {(isIngress ? "ingress" : "egress")} CIDR IP rule:");

                var rule = new CidrIpRule
                {
                    Description = GetRequiredInput("Enter description:"),
                    Port = GetRequiredInput("Enter port:"),
                    IpProtocol = GetOptionalInput("Enter IP protocol (default: TCP):", "TCP")
                };

                // Handle multiple CIDR IPs
                var cidrIps = new List<string>();
                bool continueAddingIPs = true;

                while (continueAddingIPs && cidrIps.Count < 50)
                {
                    string cidrIp = GetRequiredInput("Enter CIDR IP:");
                    cidrIps.Add(cidrIp);

                    if (cidrIps.Count < 50)
                    {
                        continueAddingIPs = GetYesNoResponse("Do you want to add another CIDR IP?");
                    }
                    else
                    {
                        Console.WriteLine("Maximum limit of 50 CIDR IPs reached.");
                    }
                }

                rule.CidrIp = cidrIps;
                rules.Add(rule);

                continueAddingRules = GetYesNoResponse($"Do you want to add another {(isIngress ? "ingress" : "egress")} CIDR IP rule?");
            }

            if (isIngress)
                zoneConfig.CidrIpIngressRules = rules;
            else
                zoneConfig.CidrIpEgressRules = rules;
        }

        private void ProcessSwimlaneRules(ZoneConfiguration zoneConfig, bool isIngress)
        {
            var rules = new List<SwimlaneRule>();
            bool continueAddingRules = true;

            while (continueAddingRules)
            {
                Console.WriteLine($"\nAdding {(isIngress ? "ingress" : "egress")} swimlane rule:");

                var rule = new SwimlaneRule
                {
                    Description = GetRequiredInput("Enter description:"),
                    Swimlane = GetRequiredInput("Enter swimlane:"),
                    Zone = GetRequiredInput("Enter zone:"),
                    Port = GetRequiredInput("Enter port:"),
                    IpProtocol = GetOptionalInput("Enter IP protocol (default: TCP):", "TCP")
                };

                rules.Add(rule);

                continueAddingRules = GetYesNoResponse($"Do you want to add another {(isIngress ? "ingress" : "egress")} swimlane rule?");
            }

            if (isIngress)
                zoneConfig.SwimlaneIngressRules = rules;
            else
                zoneConfig.SwimlaneEgressRules = rules;
        }

        private string GetRequiredInput(string prompt = "")
        {
            string input;
            do
            {
                if (!string.IsNullOrEmpty(prompt))
                    Console.WriteLine(prompt);

                input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input))
                    Console.WriteLine("This field is required. Please enter a value.");

            } while (string.IsNullOrWhiteSpace(input));

            return input;
        }

        private string GetOptionalInput(string prompt, string defaultValue)
        {
            Console.WriteLine(prompt);
            string input = Console.ReadLine()?.Trim();
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
        }

        private bool GetYesNoResponse(string prompt)
        {
            while (true)
            {
                Console.WriteLine($"{prompt} (y/n):");
                string response = Console.ReadLine()?.Trim().ToLower();

                if (response == "y" || response == "yes")
                    return true;
                if (response == "n" || response == "no")
                    return false;

                Console.WriteLine("Please enter 'y' or 'n'");
            }
        }

        private void SaveToYaml()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            var yamlBuilder = new StringBuilder();
            yamlBuilder.AppendLine("Swimlane: " + swimlaneName);

            foreach (var zone in zones)
            {
                yamlBuilder.AppendLine($"  {zone.Key}:");

                // Serialize zone configuration
                string zoneYaml = serializer.Serialize(zone.Value);
                var zoneLines = zoneYaml.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                // Add each line with proper indentation
                foreach (var line in zoneLines)
                {
                    yamlBuilder.AppendLine("    " + line);
                }
            }

            File.WriteAllText("network-flows.yaml", yamlBuilder.ToString());

            Console.WriteLine("\nNetwork flow configuration has been saved to network-flows.yaml");
        }
    }

    public class ZoneConfiguration
    {
        public List<CidrIpRule> CidrIpIngressRules { get; set; } = new List<CidrIpRule>();
        public List<CidrIpRule> CidrIpEgressRules { get; set; } = new List<CidrIpRule>();
        public List<SwimlaneRule> SwimlaneIngressRules { get; set; } = new List<SwimlaneRule>();
        public List<SwimlaneRule> SwimlaneEgressRules { get; set; } = new List<SwimlaneRule>();
    }

    public class CidrIpRule
    {
        public string Description { get; set; }
        public List<string> CidrIp { get; set; }
        public string Port { get; set; }
        public string IpProtocol { get; set; }
    }

    public class SwimlaneRule
    {
        public string Description { get; set; }
        public string Swimlane { get; set; }
        public string Zone { get; set; }
        public string Port { get; set; }
        public string IpProtocol { get; set; }
    }
}