// ***********************************
// Dotmim.Sync.Core
// SyncParameters.cs
// Modified: 02/17/2026 (Larry Leach)
// ***********************************

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Dotmim.Sync
{
    [CollectionDataContract(Name = "params", ItemName = "param"), Serializable]
    public class SyncParameters : ICollection<SyncParameter>, IList<SyncParameter>
    {
        private static string _defaultScopeHash;

        public SyncParameters()
        {
        }

        public SyncParameters(params (string Name, object Value)[] parameters)
        {
            this.AddRange(parameters.Select(p => new SyncParameter(p.Name, p.Value)));
        }

        public SyncParameters(params SyncParameter[] parameters)
        {
            this.AddRange(parameters);
        }

        [IgnoreDataMember]
        public static string DefaultScopeHash
        {
            get
            {
                if (!string.IsNullOrEmpty(_defaultScopeHash)) return _defaultScopeHash;

                var b = Encoding.UTF8.GetBytes(SyncOptions.DefaultScopeName);
                var hash1 = HashAlgorithm.SHA256.Create(b);
                _defaultScopeHash = Convert.ToBase64String(hash1);

                return _defaultScopeHash;
            }
        }

        [DataMember(Name = "c", IsRequired = true)]
        public Collection<SyncParameter> InnerCollection { get; set; } = new();

        public SyncParameter this[string name]
        {
            get
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentNullException(nameof(name));

                return this.InnerCollection.FirstOrDefault(p =>
                    string.Equals(p.Name, name, SyncGlobalization.DataSourceStringComparison));
            }
        }

        public SyncParameter this[int index]
        {
            get => this.InnerCollection[index];
            set => this.InnerCollection[index] = value;
        }

        public void Add(SyncParameter item)
        {
            if (item == null)
                return;

            if (this.Any(p => p.Name.Equals(item.Name, SyncGlobalization.DataSourceStringComparison)))
                throw new SyncParameterAlreadyExistsException(item.Name);

            this.InnerCollection.Add(item);
        }

        public void Clear()
        {
            this.InnerCollection.Clear();
        }

        public int Count => this.InnerCollection.Count;
        public bool IsReadOnly => false;

        public bool Remove(SyncParameter item)
        {
            return this.InnerCollection.Remove(item);
        }

        public bool Contains(SyncParameter item)
        {
            return this.InnerCollection.Contains(item);
        }

        public void CopyTo(SyncParameter[] array, int arrayIndex)
        {
            this.InnerCollection.CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.InnerCollection.GetEnumerator();
        }

        public IEnumerator<SyncParameter> GetEnumerator()
        {
            return this.InnerCollection.GetEnumerator();
        }

        SyncParameter IList<SyncParameter>.this[int index]
        {
            get => this.InnerCollection[index];
            set => this.InnerCollection[index] = value;
        }

        public void Insert(int index, SyncParameter item)
        {
            this.InnerCollection.Insert(index, item);
        }

        public int IndexOf(SyncParameter item)
        {
            return this.InnerCollection.IndexOf(item);
        }

        public void RemoveAt(int index)
        {
            this.InnerCollection.RemoveAt(index);
        }

        public void Add<T>(string name, T value)
        {
            this.Add(new SyncParameter(name, value));
        }

        public void AddRange(IEnumerable<SyncParameter> parameters)
        {
            if (parameters == null)
                return;

            foreach (var p in parameters)
                this.Add(p);
        }

        public bool Contains(string name)
        {
            return this[name] != null;
        }

        public string GetHash()
        {
            var flatParameters = string.Concat(this.OrderBy(p => p.Name).Select(p => $"{p.Name}.{p.Value}"));
            var b = Encoding.UTF8.GetBytes(flatParameters);
            var hash1 = HashAlgorithm.SHA256.Create(b);
            var hash1String = Convert.ToBase64String(hash1);
            return hash1String;
        }

        public bool Remove(string name)
        {
            return this.InnerCollection.Remove(this[name]);
        }

        public override string ToString()
        {
            return this.InnerCollection.Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}