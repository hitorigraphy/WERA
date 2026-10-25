using System.Text;

namespace Wera.Core;

/// <summary>TSV 파서. 큰따옴표로 감싼 셀 안의 탭/줄바꿈과 "" 이스케이프를 지원한다.</summary>
public static class Tsv
{
    /// <summary>
    /// "memo" 열은 사람이 보려고 적는 메모라 엔진은 읽지 않는다. 헤더에 있으면 그 열을 모든 행에서 뺀다
    /// (어느 마스터든 어느 위치에든 둘 수 있다).
    /// </summary>
    public static List<string[]> WithoutMemo(List<string[]> rows)
    {
        if (rows.Count == 0) return rows;
        var memo = Array.FindIndex(rows[0], h => string.Equals(h.Trim(), "memo", StringComparison.OrdinalIgnoreCase));
        if (memo < 0) return rows;
        return rows.Select(r => memo < r.Length ? r.Where((_, i) => i != memo).ToArray() : r).ToList();
    }

    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var cellStarted = false;

        if (text.Length > 0 && text[0] == '﻿') text = text[1..];

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cell.Append(c);
                continue;
            }

            switch (c)
            {
                case '"' when !cellStarted:
                    inQuotes = true;
                    cellStarted = true;
                    break;
                case '\t':
                    row.Add(cell.ToString());
                    cell.Clear();
                    cellStarted = false;
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRow();
                    break;
                default:
                    cell.Append(c);
                    cellStarted = true;
                    break;
            }
        }
        if (cellStarted || cell.Length > 0 || row.Count > 0) EndRow();
        return rows;

        void EndRow()
        {
            row.Add(cell.ToString());
            cell.Clear();
            cellStarted = false;
            if (!(row.Count == 1 && row[0].Length == 0)) rows.Add(row.ToArray());
            row.Clear();
        }
    }
}
