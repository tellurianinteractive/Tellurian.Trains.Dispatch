namespace Tellurian.Trains.Dispatch.Utilities;

public readonly struct Option<T>
{
    internal Option(T? value) { Value = value; }
    public T? Value { get; }
    public IEnumerable<string> Errors { get; init; } = [];
    public string Error => string.Join(", ", Errors);
    public bool HasValue => Value is not null;
    public bool IsFail => Value is null;
}

public static class OptionExtensions
{
    extension<T>(Option<T> option)
    {
        public static Option<T> Fail(params string[] errors) => new(default) { Errors = errors };
        public static Option<T> Fail(string error) => new(default) { Errors = [error] };

        public static Option<T> Success(T Value) => new(Value);

        public static Option<T> Create(T? Value, params string[] errors) =>
            Value is not null ? Success(Value) : Fail<T>(errors);
    }
}



