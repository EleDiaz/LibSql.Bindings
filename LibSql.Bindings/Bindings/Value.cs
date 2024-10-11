namespace LibSql.Bindings;

public abstract class Value { 
    public static Value? FromObject(object? obj) {
        return obj switch {
            Value val => val,
            int val => new IntValue(val),
            string val => new StringValue(val),
            double val => new FloatValue(val),
            float val => new FloatValue(val),
            byte[] val => new BlobValue(val),
            null => null,
            _ => throw new LibSqlException("No conversion available to Value from: " + obj.GetType())
        };
    }
}

public class IntValue : Value
{
    public int Value { get; }

    public IntValue(int value)
    {
        Value = value;
    }

    public static implicit operator IntValue(int value)
    {
        return new IntValue(value);
    }
}

public class StringValue : Value
{
    public string Value { get; }

    public StringValue(string value)
    {
        Value = value;
    }

    public static implicit operator StringValue(string value) => new StringValue(value);

    public static implicit operator string(StringValue stringValue)
    {
        return stringValue.Value;
    }
}

public class FloatValue : Value
{
    public double Value { get; }

    public FloatValue(double value)
    {
        Value = value;
    }

    public static implicit operator FloatValue(double value)
    {
        return new FloatValue(value);
    }
}

public class BlobValue : Value
{
    public byte[] Value { get; }

    public BlobValue(byte[] value)
    {
        Value = value;
    }

    public static implicit operator BlobValue(byte[] value)
    {
        return new BlobValue(value);
    }
}
