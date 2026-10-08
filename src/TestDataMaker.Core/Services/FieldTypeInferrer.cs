using System.Globalization;
using TestDataMaker.Models;

namespace TestDataMaker.Services;

/// <summary>
/// CSV の列名（と先頭データ）からデータ型と初期設定を推測する。
/// 読み込み直後の手直しを減らすためのもので、推測が外れても利用者が画面で変更できる。
/// </summary>
public static class FieldTypeInferrer
{
    public static List<FieldDefinition> Infer(CsvReadResult csv)
    {
        var fields = new List<FieldDefinition>();
        for (int i = 0; i < csv.Headers.Length; i++)
        {
            var samples = csv.SampleRows.Where(r => i < r.Length).Select(r => r[i].Trim()).Where(s => s.Length > 0).ToList();
            fields.Add(Infer(csv.Headers[i], samples, isFirstColumn: i == 0));
        }
        return fields;
    }

    public static FieldDefinition Infer(string name, IReadOnlyList<string> samples, bool isFirstColumn = false)
    {
        string n = name.Trim().ToUpperInvariant();
        bool Has(params string[] keys) => keys.Any(k => n.Contains(k, StringComparison.Ordinal));
        bool EndsWith(params string[] keys) => keys.Any(k => n.EndsWith(k, StringComparison.Ordinal));

        if (Has("MAIL", "メール"))
            return FieldDefinition.Create(name, DataType.Email);
        if (Has("TEL", "PHONE", "電話", "携帯", "FAX"))
            return FieldDefinition.Create(name, DataType.Phone);
        if (Has("ZIP", "POSTAL", "POST_CODE", "POSTCODE", "郵便"))
            return FieldDefinition.Create(name, DataType.PostalCode);
        if (Has("ADDRESS", "ADDR", "住所", "所在地"))
            return FieldDefinition.Create(name, DataType.Address);
        if (Has("KANA", "カナ", "フリガナ", "ふりがな"))
            return Name(name, NameFormat.FullNameKana);
        if (n is "NAME" or "FULL_NAME" or "FULLNAME" or "USER_NAME" or "USERNAME" or "CUSTOMER_NAME" or "EMPLOYEE_NAME"
            || Has("氏名", "姓名", "名前", "担当者"))
            return Name(name, NameFormat.FullName);
        if (Has("LAST_NAME", "LASTNAME", "FAMILY_NAME", "姓"))
            return Name(name, NameFormat.LastName);
        if (Has("FIRST_NAME", "FIRSTNAME", "GIVEN_NAME"))
            return Name(name, NameFormat.FirstName);
        if (Has("BIRTH", "生年月日", "誕生"))
        {
            var f = FieldDefinition.Create(name, DataType.Date);
            f.StartDate = DateTime.Today.AddYears(-80);
            f.EndDate = DateTime.Today.AddYears(-18);
            return f;
        }
        if (Has("DATE", "日付", "年月日", "日時") || EndsWith("_AT", "_DT", "日"))
        {
            var f = FieldDefinition.Create(name, DataType.Date);
            f.StartDate = DateTime.Today.AddYears(-1);
            f.EndDate = DateTime.Today;
            if (Has("TIME", "日時") || EndsWith("_AT")) f.DateFormat = "yyyy/MM/dd HH:mm:ss";
            return f;
        }
        if (Has("AGE", "年齢"))
        {
            var f = FieldDefinition.Create(name, DataType.Number);
            f.MinValue = 18;
            f.MaxValue = 80;
            return f;
        }
        if (n is "ID" || EndsWith("_ID", "ID") && isFirstColumn || Has("番号", "NO") && isFirstColumn)
            return FieldDefinition.Create(name, DataType.Sequence);
        if (Has("PRICE", "AMOUNT", "金額", "価格", "単価"))
        {
            var f = FieldDefinition.Create(name, DataType.Number);
            f.MinValue = 100;
            f.MaxValue = 100000;
            return f;
        }
        if (Has("STATUS", "FLAG", "FLG", "区分", "状態"))
        {
            var f = FieldDefinition.Create(name, DataType.Fixed);
            f.FixedValue = samples.FirstOrDefault() ?? (Has("STATUS") ? "ACTIVE" : "0");
            return f;
        }

        // 列名で判断できない場合はサンプルデータから推測する
        if (samples.Count > 0)
        {
            if (samples.All(s => DateTime.TryParse(s, CultureInfo.GetCultureInfo("ja-JP"), DateTimeStyles.None, out _) && s.Any(char.IsAsciiDigit) && s.Length >= 8))
                return FieldDefinition.Create(name, DataType.Date);
            var numbers = samples.Select(s => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null).ToList();
            if (numbers.All(d => d is not null))
            {
                var f = FieldDefinition.Create(name, DataType.Number);
                decimal max = numbers.Max()!.Value;
                f.MinValue = Math.Min(0, numbers.Min()!.Value);
                f.MaxValue = max <= 0 ? 100 : Math.Max(100, Math.Ceiling(max * 2));
                f.DecimalPlaces = Math.Min(samples.Max(s => s.Contains('.') ? s.Length - s.IndexOf('.') - 1 : 0), 6);
                return f;
            }
        }
        return FieldDefinition.Create(name, DataType.String);
    }

    private static FieldDefinition Name(string name, NameFormat format)
    {
        var f = FieldDefinition.Create(name, DataType.JapaneseName);
        f.NameFormat = format;
        return f;
    }
}
