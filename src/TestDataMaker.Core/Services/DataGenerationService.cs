using TestDataMaker.Generators;
using TestDataMaker.Models;

namespace TestDataMaker.Services;

/// <summary>
/// 定義に従って行データを生成する。
/// 行は必要な分だけ順次生成する（IEnumerable）ため、100 万件でも全件をメモリに持たない。
/// NULL は null で表す。
/// </summary>
public sealed class DataGenerationService
{
    /// <summary>重複禁止時、ランダム生成で未使用の値を探す回数。超えたら重複なし抽選に切り替える。</summary>
    private const int RandomRetryLimit = 50;

    public IEnumerable<string?[]> GenerateRows(
        IReadOnlyList<FieldDefinition> fields,
        long count,
        GenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 引数エラーは列挙開始前ではなく呼び出し時に検出したいので、イテレータと分ける
        if (fields.Count == 0) throw new TestDataException("項目を1つ以上定義してください。");
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var random = options?.Seed is int seed ? new Random(seed) : new Random();
        var states = fields.Select(f => new FieldState(f, count)).ToArray();
        return Iterate(states, count, random, cancellationToken);
    }

    private static IEnumerable<string?[]> Iterate(FieldState[] states, long count, Random random, CancellationToken ct)
    {
        for (long row = 0; row < count; row++)
        {
            if ((row & 0x3FF) == 0) ct.ThrowIfCancellationRequested();
            var values = new string?[states.Length];
            for (int i = 0; i < states.Length; i++)
                values[i] = states[i].Next(random, row);
            yield return values;
        }
    }

    private sealed class FieldState
    {
        private readonly FieldDefinition _field;
        private readonly long _count;
        private readonly ValueGenerator _generator;
        private readonly NullGenerator _null;
        private readonly HashSet<string>? _used;
        private UniqueIndexSampler? _sampler;

        public FieldState(FieldDefinition field, long count)
        {
            _field = field;
            _count = count;
            try
            {
                _generator = GeneratorFactory.Create(field);
            }
            catch (ArgumentException ex)
            {
                throw new TestDataException($"項目「{field.Name}」の設定が正しくありません。{ex.Message}", ex);
            }
            _null = new NullGenerator(field.NullRate);
            if (field.Unique && !_generator.IsInherentlyUnique)
                _used = new HashSet<string>(StringComparer.Ordinal);
        }

        public string? Next(Random random, long row)
        {
            if (_null.ShouldBeNull(random)) return null;
            if (_used is null) return _generator.Generate(random, row);

            if (_sampler is null)
            {
                for (int i = 0; i < RandomRetryLimit; i++)
                {
                    string v = _generator.Generate(random, row);
                    if (_used.Add(v)) return v;
                }
                // 値が埋まってきたので、未抽選のインデックスから重複なく選ぶ方式に切り替える
                _sampler = new UniqueIndexSampler(_generator.DomainSize);
            }

            while (_sampler.TryNext(random, out long index))
            {
                string v = _generator.GetValue(index);
                if (_used.Add(v)) return v;
            }
            throw new TestDataException(
                $"指定された条件では{_count}件のユニークデータを生成できません。\n" +
                $"項目「{_field.Name}」の重複禁止を解除するか、生成範囲を広げてください。");
        }
    }
}

/// <summary>
/// 0～size-1 の整数を重複なくランダムな順に取り出す（疎な Fisher-Yates シャッフル）。
/// 使用メモリは取り出した件数に比例し、size が巨大でも扱える。
/// </summary>
internal sealed class UniqueIndexSampler
{
    private readonly Dictionary<long, long> _swapped = new();
    private long _remaining;

    public UniqueIndexSampler(long size)
    {
        _remaining = size;
    }

    public bool TryNext(Random random, out long value)
    {
        if (_remaining <= 0)
        {
            value = 0;
            return false;
        }
        long r = random.NextInt64(_remaining);
        long last = _remaining - 1;
        value = _swapped.TryGetValue(r, out long sr) ? sr : r;
        long lastValue = _swapped.TryGetValue(last, out long sl) ? sl : last;
        if (r != last) _swapped[r] = lastValue;
        _swapped.Remove(last);
        _remaining--;
        return true;
    }
}
