using TestDataMaker.Models;

namespace TestDataMaker.Generators;

/// <summary>項目定義から対応する Generator を作る。新しいデータ型はここに追加する。</summary>
public static class GeneratorFactory
{
    public static ValueGenerator Create(FieldDefinition field) => field.DataType switch
    {
        DataType.String => new StringGenerator(StringGenerator.CharsOf(field.CharSet, field.CustomChars), field.MinLength, field.MaxLength, field.Prefix),
        DataType.JapaneseName => new NameGenerator(field.NameFormat),
        DataType.Number => new NumberGenerator(field.MinValue, field.MaxValue, field.DecimalPlaces),
        DataType.Sequence => new SequenceGenerator(field.StartValue, field.Increment),
        DataType.Date => new DateGenerator(field.StartDate, field.EndDate, field.DateFormat),
        DataType.Email => new EmailGenerator(field.EmailDomain, field.EmailMode),
        DataType.Phone => new PhoneGenerator(field.PhoneType),
        DataType.PostalCode => new PostalCodeGenerator(),
        DataType.Address => new AddressGenerator(),
        DataType.Fixed => new FixedGenerator(field.FixedValue),
        _ => throw new NotSupportedException($"未対応のデータ型です: {field.DataType}"),
    };
}
