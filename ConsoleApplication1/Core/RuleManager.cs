using System;
using System.Collections.Generic;
using System.IO;

namespace Ruley.Core
{
    public class RuleManager
    {
        public static MessageBus MessageBus = new MessageBus();

        private readonly Dictionary<string, Rule> _rules = new Dictionary<string, Rule>();
        private readonly RuleProvider _ruleProvider;
        private readonly Preprocessor _preprocessor;

        private List<string> _ignoreFile = new List<string>() { "global" };

        public RuleManager()
        {
            //todo: autogenerate
            _preprocessor = new Preprocessor();
            _preprocessor.Alias("@slack", "Ruley.Core.Outputs.SlackOutput, Ruley");
            _preprocessor.Alias("@map", "Ruley.Core.Filters.MapFilter, Ruley");
            _preprocessor.Alias("@throttle", "Ruley.Core.Filters.ThrottleFilter, Ruley");
            _preprocessor.Alias("@concat", "Ruley.Core.Filters.ConcatFilter, Ruley");
            _preprocessor.Alias("@conditional", "Ruley.Core.Filters.ConditionalFilter, Ruley");
            _preprocessor.Alias("@addfield", "Ruley.Core.Filters.AddFieldFilter, Ruley");
            _preprocessor.Alias("@chain", "Ruley.Core.Filters.ChainFilter, Ruley");
            _preprocessor.Alias("@groupby", "Ruley.Core.Filters.GroupByFilter, Ruley");
            _preprocessor.Alias("@calc", "Ruley.Core.Filters.CalcFilter, Ruley");
            _preprocessor.Alias("@template", "Ruley.Core.Filters.TemplateFilter, Ruley");
            _preprocessor.Alias("@prev", "Ruley.Core.Filters.PrevFilter, Ruley");
            _preprocessor.Alias("@count", "Ruley.Core.Filters.CountFilter, Ruley");
            _preprocessor.Alias("@slackf", "Ruley.Core.Filters.SlackFilter, Ruley");
            _preprocessor.Alias("@passthrough", "Ruley.Core.Filters.PassThroughFilter, Ruley");
            _preprocessor.Alias("@messagebuspublisher", "Ruley.Core.Filters.MessageBusPublisherFilter, Ruley");
            _preprocessor.Alias("@branch", "Ruley.Core.Filters.BranchFilter, Ruley");

            _preprocessor.Alias("@console", "Ruley.Core.Outputs.ConsoleOutput, Ruley");

            _preprocessor.Alias("@slack_url", "https://hooks.slack.com/services/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");

            _ruleProvider = new RuleProvider(_preprocessor);
        }

        public void StartFileWatcher()
        {
            var watcher = new FileSystemWatcher("rules");
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName | NotifyFilters.CreationTime;

            watcher.Filter = "*.json";

            var w = new ObservableFileWatcher(watcher);

            w.Changed.Subscribe(fn =>
            {
                try
                {
                    lock (_rules)
                    {
                        if (_rules.ContainsKey(fn))
                        {
                            Console.WriteLine("File change detected ({0})", fn);
                            _rules[fn].Dispose();

                            var rule = _ruleProvider.Create(fn);
                            _rules[fn] = rule;
                            _rules[fn].Start();
                        }
                        else
                        {
                            Console.WriteLine("Couldn't change rule {0}", fn);
                        }
                    }
                }
                catch (Exception e)
                {

                }
            });

            w.Created.Subscribe(fn =>
            {
                lock (_rules)
                {
                    if (!_rules.ContainsKey(fn))
                    {
                        Console.WriteLine("File added detected ({0})", fn);
                        _rules[fn] = _ruleProvider.Create(fn);
                        _rules[fn].Start();
                    }
                }
            });

            w.Deleted.Subscribe(fn =>
            {
                lock (_rules)
                {
                    if (_rules.ContainsKey(fn))
                    {
                        Console.WriteLine("File deleted detected ({0})", fn);
                        _rules[fn].Dispose();
                        Console.WriteLine("Removing rule ({0})", fn);
                        _rules.Remove(fn);
                    }
                }
            });
        }

        public void Start()
        {
            lock (_rules)
            {
                foreach (var fn in Directory.GetFiles("rules"))
                {
                    try
                    {
                        if (_ignoreFile.Contains(fn))
                            continue;

                        _rules[fn] = _ruleProvider.Create(fn);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(String.Format("Failed to load rule {0}", e.Message));
                    }
                }

                foreach (var rulePair in _rules)
                {
                    rulePair.Value.Start();
                }
            }
        }
    }
}