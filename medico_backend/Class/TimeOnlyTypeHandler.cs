using Dapper;
using System.Data;

public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.Value = value.ToTimeSpan(); // ✅ Write: TimeOnly → TimeSpan
        parameter.DbType = DbType.Time;
    }

    public override TimeOnly Parse(object value)
    {
        return TimeOnly.FromTimeSpan((TimeSpan)value); // ✅ Read: TimeSpan → TimeOnly
    }
}