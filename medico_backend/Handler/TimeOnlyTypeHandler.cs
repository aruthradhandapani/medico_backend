using Dapper;
using System.Data;

namespace Medico_Backend.Handlers
{
    public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
    {
        public override void SetValue(IDbDataParameter parameter, TimeOnly value)
        {
            parameter.Value = value.ToTimeSpan();
        }

        public override TimeOnly Parse(object value)
        {
            return value switch
            {
                TimeOnly t => t,
                TimeSpan ts => TimeOnly.FromTimeSpan(ts),
                DateTime dt => TimeOnly.FromDateTime(dt),
                string s => TimeOnly.Parse(s),
                _ => throw new DataException($"Cannot convert {value?.GetType().Name ?? "null"} to TimeOnly")
            };
        }
    }

    public class NullableTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, TimeOnly? value)
        {
            parameter.Value = value.HasValue ? (object)value.Value.ToTimeSpan() : DBNull.Value;
        }

        public override TimeOnly? Parse(object value)
        {
            if (value == null || value is DBNull) return null;

            return value switch
            {
                TimeOnly t => t,
                TimeSpan ts => TimeOnly.FromTimeSpan(ts),
                DateTime dt => TimeOnly.FromDateTime(dt),
                string s => TimeOnly.Parse(s),
                _ => throw new DataException($"Cannot convert {value.GetType().Name} to TimeOnly?")
            };
        }
    }
}