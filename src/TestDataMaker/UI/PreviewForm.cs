using TestDataMaker.Models;
using TestDataMaker.Services;

namespace TestDataMaker.UI;

/// <summary>生成前に先頭の数十件を確認するためのプレビュー画面。</summary>
internal sealed class PreviewForm : Form
{
    public const int PreviewCount = 100;

    private readonly IReadOnlyList<FieldDefinition> _fields;
    private readonly int _count;
    private readonly DataGridView _grid = new();
    private readonly Label _info = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) };

    public PreviewForm(IReadOnlyList<FieldDefinition> fields, int recordCount)
    {
        _fields = fields;
        _count = Math.Min(PreviewCount, recordCount);

        Text = "プレビュー - TestDataMaker";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1000, 600);
        MinimumSize = new Size(500, 300);
        ShowInTaskbar = false;
        KeyPreview = true;

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersWidth = 60;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText;
        _grid.DefaultCellStyle.NullValue = "(NULL)";
        _grid.CellFormatting += (_, e) =>
        {
            if (e.Value is null && e.CellStyle is not null) e.CellStyle.ForeColor = SystemColors.GrayText;
        };

        var regenerate = new Button { Text = "再生成(&R)", AutoSize = true };
        regenerate.Click += (_, _) => Generate();
        var close = new Button { Text = "閉じる", AutoSize = true, DialogResult = DialogResult.OK };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.Add(close);
        buttons.Controls.Add(regenerate);
        buttons.Controls.Add(_info);

        Controls.Add(_grid);
        Controls.Add(buttons);
        AcceptButton = close;
        CancelButton = close;

        Load += (_, _) => Generate();
    }

    private void Generate()
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            _grid.SuspendLayout();
            _grid.Rows.Clear();
            _grid.Columns.Clear();
            foreach (var f in _fields)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = f.Name,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                };
                if (f.DataType is DataType.Number or DataType.Sequence)
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _grid.Columns.Add(col);
            }

            int n = 0;
            foreach (var row in new DataGenerationService().GenerateRows(_fields, _count))
            {
                int index = _grid.Rows.Add(row.Cast<object?>().ToArray());
                _grid.Rows[index].HeaderCell.Value = (++n).ToString();
            }
            _grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            foreach (DataGridViewColumn c in _grid.Columns) c.Width = Math.Min(c.Width, 300);

            _info.Text = $"先頭 {n} 件を表示しています。（毎回ランダムに生成されるため、実際の出力とは値が異なります）";
        }
        catch (TestDataException ex)
        {
            MessageBox.Show(this, ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _grid.ResumeLayout();
            Cursor = Cursors.Default;
        }
    }
}
