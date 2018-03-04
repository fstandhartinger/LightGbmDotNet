using System.Collections.Generic;

namespace LightGbmDotNet
{
    public class Parameter
    {
        public Parameter()
        {
        }

        public Parameter(string id, string value)
        {
            Id = id;
            Value = value;
        }

        public string Id { get; set; }
        public string Value { get; set; }

        public static IEqualityComparer<Parameter> IdComparer { get; } = new IdEqualityComparer();

        public override string ToString()
        {
            return $"{Id}={Value}";
        }

        protected bool Equals(Parameter other)
        {
            return string.Equals(Id, other.Id) && string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Parameter) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Id != null ? Id.GetHashCode() : 0) * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }

        private sealed class IdEqualityComparer : IEqualityComparer<Parameter>
        {
            public bool Equals(Parameter x, Parameter y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x.Id, y.Id);
            }

            public int GetHashCode(Parameter obj)
            {
                return obj.Id != null ? obj.Id.GetHashCode() : 0;
            }
        }
    }
}