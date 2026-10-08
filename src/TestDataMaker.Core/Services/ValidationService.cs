using System.Globalization;
using System.Text.RegularExpressions;
using TestDataMaker.Generators;
using TestDataMaker.Models;

namespace TestDataMaker.Services;

public sealed class ValidationResult
{
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public override string ToString() => string.Join(Environment.NewLine, Errors);
}

/// <summary>生成前の入力チェック。エラーメッセージは画面にそのまま表示する。</summary>
public sealed partial class ValidationService
{
    public const int MinRecordCount = 1;
    public const int MaxRecordCount = 1_000_000;
    public const int MaxStringLength = 1000;
    public const int MaxFieldNameLength = 128;

    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)+$")]
    private static partial Regex DomainRegex();

    /// <summary>画面の生成件数入力を検証して数値に変換する。</summary>
    public static bool TryParseRecordCount(string? text, out int count, out string error)
    {
        count = 0;
        error = "";
        string s = (text ?? "").Trim().Replace(",", "").Replace("，", "");
        if (s.Length == 0)
        {
            error = "生成件数を入力してください。";
            return false;
        }
        if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n))
        {
            error = "生成件数は半角数字で入力してください。";
            return false;
        }
        if (n < MinRecordCount)
        {
            error = "生成件数は1以上にしてください。";
            return false;
        }
        if (n > MaxRecordCount)
        {
            error = $"生成件数は{MaxRecordCount:N0}以下にしてください。";
            return false;
        }
        count = (int)n;
        return true;
    }

    public ValidationResult Validate(IReadOnlyList<FieldDefinition> fields, long recordCount)
    {
        var result = new ValidationResult();

        if (recordCount < MinRecordCount) result.Errors.Add("生成件数は1以上にしてください。");
        else if (recordCount > MaxRecordCount) result.Errors.Add($"生成件数は{MaxRecordCount:N0}以下にしてください。");

        if (fields.Count == 0)
        {
            result.Errors.Add("項目を1つ以上定義してください。");
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            string label = string.IsNullOrWhiteSpace(f.Name) ? $"{i + 1}行目" : $"項目「{f.Name}」";

            if (string.IsNullOrWhiteSpace(f.Name))
                result.Errors.Add($"{i + 1}行目：項目名を入力してください。");
            else if (f.Name.Length > MaxFieldNameLength)
                result.Errors.Add($"{label}：項目名は{MaxFieldNameLength}文字以内にしてください。");
            else if (!seen.Add(f.Name.Trim()))
                result.Errors.Add($"{label}：項目名が重複しています。");

            ValidateField(f, label, Math.Max(recordCount, 0), result.Errors);
        }
        return result;
    }

    private static void ValidateField(FieldDefinition f, string label, long recordCount, List<string> errors)
    {
        int before = errors.Count;

        if (double.IsNaN(f.NullRate) || f.NullRate < 0 || f.NullRate > 100)
            errors.Add($"{label}：NULL率は0～100%の範囲で指定してください。");

        switch (f.DataType)
        {
            case DataType.String:
                if (f.MinLength < 0 || f.MaxLength < 0)
                    errors.Add($"{label}：文字数は0以上にしてください。");
                else if (f.MinLength > f.MaxLength)
                    errors.Add($"{label}：最小文字数は最大文字数以下に設定してください。");
                else if (f.MaxLength > MaxStringLength)
                    errors.Add($"{label}：最大文字数は{MaxStringLength}以下にしてください。");
                if (string.IsNullOrEmpty(StringGenerator.CharsOf(f.CharSet, f.CustomChars)))
                    errors.Add($"{label}：使用文字を入力してください。");
                break;

            case DataType.Number:
                if (f.MinValue > f.MaxValue)
                    errors.Add($"{label}：最小値は最大値以下に設定してください。");
                else if (f.DecimalPlaces < 0 || f.DecimalPlaces > NumberGenerator.MaxDecimalPlaces)
                    errors.Add($"{label}：小数桁数は0～{NumberGenerator.MaxDecimalPlaces}の範囲で指定してください。");
                else
                {
                    long? n = NumberGenerator.CountValues(f.MinValue, f.MaxValue, f.DecimalPlaces);
                    if (n == 0) errors.Add($"{label}：指定された範囲と小数桁数では生成できる数値がありません。");
                    else if (n is null) errors.Add($"{label}：数値の範囲が大きすぎます。範囲または小数桁数を小さくしてください。");
                }
                break;

            case DataType.Sequence:
                if (f.Increment == 0)
                    errors.Add($"{label}：増分は0以外を指定してください。");
                else if (!SequenceGenerator.Fits(f.StartValue, f.Increment, recordCount))
                    errors.Add($"{label}：連番が数値の上限を超えます。開始値・増分を見直してください。");
                break;

            case DataType.Date:
                if (f.StartDate.Date > f.EndDate.Date)
                    errors.Add($"{label}：開始日は終了日以前に設定してください。");
                if (!DateGenerator.IsValidFormat(f.DateFormat))
                    errors.Add($"{label}：日付形式が正しくありません。（例：yyyy/MM/dd）");
                break;

            case DataType.Email:
                if (!DomainRegex().IsMatch((f.EmailDomain ?? "").Trim().TrimStart('@')))
                    errors.Add($"{label}：メールアドレスのドメインが正しくありません。（例：example.com）");
                break;
        }

        // 個別設定にエラーがなければ、重複禁止で必要な件数を生成できるか確認する
        if (errors.Count == before && f.Unique && recordCount > 0)
        {
            ValueGenerator gen;
            try
            {
                gen = GeneratorFactory.Create(f);
            }
            catch (ArgumentException ex)
            {
                errors.Add($"{label}：{ex.Message}");
                return;
            }
            if (gen.IsInherentlyUnique) return;

            // NULL は重複の対象外。NULL 率が 0 のときのみ厳密に判定し、それ以外は生成時に検出する
            long needed = f.NullRate <= 0 ? recordCount : (long)Math.Ceiling(recordCount * (100 - f.NullRate) / 100 * 0.9);
            if (needed > 0 && gen.DomainSize < needed)
            {
                errors.Add($"{label}：指定された条件では{recordCount}件のユニークデータを生成できません。" +
                           $"（生成可能な値は{gen.DomainSize:N0}種類です）");
            }
        }
    }
}
