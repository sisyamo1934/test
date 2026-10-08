using TestDataMaker.Data;

namespace TestDataMaker.Generators;

/// <summary>都道府県＋市区町村＋町名＋丁目番地号のダミー住所を生成する。</summary>
public sealed class AddressGenerator : ValueGenerator
{
    private const int Chome = 5;
    private const int Ban = 30;
    private const int Go = 20;

    private readonly string[] _cities = AddressData.Cities;
    private readonly string[] _towns = AddressData.Towns;

    public override long DomainSize => (long)_cities.Length * _towns.Length * Chome * Ban * Go;

    public override string Generate(Random random, long rowIndex) => GetValue(random.NextInt64(DomainSize));

    public override string GetValue(long index)
    {
        long go = index % Go + 1; index /= Go;
        long ban = index % Ban + 1; index /= Ban;
        long chome = index % Chome + 1; index /= Chome;
        string town = _towns[index % _towns.Length]; index /= _towns.Length;
        string city = _cities[index];
        return $"{city}{town}{chome}丁目{ban}-{go}";
    }
}
