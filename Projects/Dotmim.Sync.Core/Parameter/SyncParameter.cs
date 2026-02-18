// ***********************************
// Dotmim.Sync.Core
// SyncParameter.cs
// Modified: 02/17/2026 (Larry Leach)
// ***********************************

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Dotmim.Sync
{
    [DataContract(Name = "par"), Serializable]
    public class SyncParameter : SyncNamedItem<SyncParameter>
    {
        public SyncParameter()
        {
        }

        public SyncParameter(string name, object value)
        {
            this.Name = name;
            this.Value = value?.ToString() ?? "";
        }

        [DataMember(Name = "pn", IsRequired = true, Order = 1)]
        public string Name { get; set; }

        [DataMember(Name = "v", IsRequired = true, Order = 2)]
        public object Value { get; set; }

        public override IEnumerable<string> GetAllNamesProperties()
        {
            yield return this.Name;
        }

        public override string ToString()
        {
            return $"{this.Name}: {this.Value}";
        }
    }
}