using System.Text;
using System.Text.Json;
using ClosedXML.Excel;

namespace DigitMak.Portal.Api.Application.Exports;

public static class SpreadsheetExports
{
    private static readonly IReadOnlyDictionary<string, string> TemplateTitles = new Dictionary<
        string,
        string
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["TICKET-RESOLUTION"] = "Затворање тикет и конечна препорака",
        ["MEETING-DELIVERY"] = "Одржан консултативен состанок",
        ["SUBSCRIPTION-KPI"] = "Активна годишна претплата",
        ["CONTACT-INTAKE"] = "Прием и обработка на контакт-барање",
        ["KPI-PERIOD"] = "KPI досие за извештаен период",
        ["KPI-CONTACT-BREAKDOWN"] = "Преглед на контакт-барања",
        ["KPI-TICKET-BREAKDOWN"] = "Преглед на тикети за поддршка",
        ["KPI-MEETING-REFERRAL"] = "Состаноци и упатувања",
        ["KPI-SUBSCRIPTION-COHORT"] = "Преглед на претплати",
    };

    private static readonly IReadOnlyDictionary<string, (string Label, string Example)> FieldCopy =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["organizationType"] = ("Тип на организација", "МСП, јавна институција, стартап..."),
            ["sector"] = ("Сектор", "Информатичка технологија"),
            ["region"] = ("Регион", "Скопски регион"),
            ["mainNeed"] = ("Главна потреба", "Краток опис на потребата"),
            ["handledAt"] = ("Датум на обработка", "2026-07-22"),
            ["ticketNumber"] = ("Број на тикет", "DM-2026-0001"),
            ["category"] = ("Категорија", "AI подготвеност"),
            ["finalRecommendation"] = ("Конечна препорака", "Кратка практична препорака"),
            ["resolvedAt"] = ("Датум на затворање", "2026-07-22"),
            ["meetingType"] = ("Тип на состанок", "Онлајн или во живо"),
            ["startsAt"] = ("Почеток", "2026-07-24 13:00"),
            ["endsAt"] = ("Крај", "2026-07-24 14:00"),
            ["attendees"] = ("Учесници", "Имиња или број на учесници"),
            ["outcome"] = ("Резултат", "Договорени следни чекори"),
            ["userId"] = ("Корисник ID", "UUID на корисникот"),
            ["organizationId"] = ("Организација ID", "UUID на организацијата"),
            ["expiresAt"] = ("Важи до", "2027-07-22"),
            ["paymentReference"] = ("Референца за уплата", "Број на фактура или уплата"),
            ["reportingPeriod"] = ("Извештаен период", "2026-Q3"),
            ["kpiCategory"] = ("KPI категорија", "AI_HELP_DESK"),
            ["metricValue"] = ("Вредност на показател", "25"),
            ["source"] = ("Извор", "Системски извештај или документ"),
            ["approvedBy"] = ("Одобрил/а", "Име и функција"),
            ["totalContacts"] = ("Вкупно контакт-барања", "0"),
            ["bySector"] = ("По сектор", "ИТ: 0; Производство: 0"),
            ["byRegion"] = ("По регион", "Скопски: 0; Полошки: 0"),
            ["byOrganizationType"] = ("По тип на организација", "МСП: 0; Јавни: 0"),
            ["byDmaCategory"] = ("По DMA категорија", "Категорија: 0"),
            ["sourceQuery"] = ("Извор / филтер", "Назив на извештајот и применети филтри"),
            ["totalTickets"] = ("Вкупно тикети", "0"),
            ["byCategory"] = ("По категорија", "AI подготвеност: 0"),
            ["byStatus"] = ("По статус", "Отворени: 0; Затворени: 0"),
            ["byPriority"] = ("По приоритет", "Нормален: 0; Висок: 0"),
            ["byAssignee"] = ("По одговорно лице", "Име: 0"),
            ["requested"] = ("Побарани состаноци", "0"),
            ["completed"] = ("Завршени состаноци", "0"),
            ["byMeetingType"] = ("По тип на состанок", "Онлајн: 0; Во живо: 0"),
            ["referralsByDestination"] = ("Упатувања по дестинација", "Организација: 0"),
            ["invited"] = ("Поканети претплатници", "0"),
            ["activated"] = ("Активирани претплати", "0"),
            ["expired"] = ("Истечени претплати", "0"),
            ["cancelled"] = ("Откажани претплати", "0"),
            ["activeAtPeriodEnd"] = ("Активни на крајот на периодот", "0"),
        };

    public static byte[] EvidenceTemplate(EvidenceTemplate template)
    {
        var fields = JsonSerializer.Deserialize<string[]>(template.RequiredMetadataJson) ?? [];
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("KPI образец");
        sheet.ShowGridLines = false;

        sheet.Range("A1:C1").Merge();
        sheet.Cell("A1").Value = "DigitMak · KPI документација";
        sheet.Cell("A2").Value = "Образец";
        sheet.Range("B2:C2").Merge();
        sheet.Cell("B2").Value = TemplateTitles.GetValueOrDefault(template.Code, template.Name);
        sheet.Cell("A3").Value = "Код";
        sheet.Range("B3:C3").Merge();
        sheet.Cell("B3").Value = template.Code;
        sheet.Cell("A4").Value = "Упатство";
        sheet.Range("B4:C4").Merge();
        sheet.Cell("B4").Value =
            "Пополнете ја колоната „Вредност“. Не ги менувајте називите во првата колона.";

        const int headerRow = 6;
        sheet.Cell(headerRow, 1).Value = "Поле";
        sheet.Cell(headerRow, 2).Value = "Вредност";
        sheet.Cell(headerRow, 3).Value = "Пример / појаснување";

        for (var index = 0; index < fields.Length; index++)
        {
            var row = headerRow + index + 1;
            var field = fields[index];
            var copy = FieldCopy.GetValueOrDefault(
                field,
                (Label: Humanize(field), Example: "Внесете вредност")
            );
            sheet.Cell(row, 1).Value = copy.Label;
            sheet.Cell(row, 3).Value = copy.Example;
        }

        var lastRow = Math.Max(headerRow + fields.Length, headerRow + 1);
        var brandRed = XLColor.FromHtml("#D11525");
        var paleRed = XLColor.FromHtml("#FFF3F4");
        var line = XLColor.FromHtml("#D9DEE2");

        sheet
            .Range("A1:C1")
            .Style.Fill.SetBackgroundColor(paleRed)
            .Font.SetFontColor(brandRed)
            .Font.SetBold()
            .Font.SetFontSize(18);
        sheet.Range("A1:C1").Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 34;
        sheet.Range("A2:A4").Style.Font.SetBold();
        sheet.Range("A2:C4").Style.Fill.SetBackgroundColor(paleRed);
        sheet.Range("A2:C4").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range("A2:C4").Style.Border.OutsideBorderColor = line;
        sheet
            .Range(headerRow, 1, headerRow, 3)
            .Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F1F3F4"))
            .Font.SetFontColor(XLColor.FromHtml("#172126"))
            .Font.SetBold();
        sheet.Range(headerRow + 1, 1, lastRow, 3).Style.Border.BottomBorder =
            XLBorderStyleValues.Thin;
        sheet.Range(headerRow + 1, 1, lastRow, 3).Style.Border.BottomBorderColor = line;
        sheet.Range(headerRow + 1, 1, lastRow, 1).Style.Font.SetBold();
        sheet
            .Range(headerRow + 1, 2, lastRow, 2)
            .Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FFFBEA"));
        sheet.Range("A1:C4").Style.Alignment.WrapText = true;
        sheet.Range(headerRow, 1, lastRow, 3).Style.Alignment.WrapText = true;
        sheet.Column(1).Width = 29;
        sheet.Column(2).Width = 34;
        sheet.Column(3).Width = 48;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] EvidenceTemplateCsv(EvidenceTemplate template)
    {
        var fields = JsonSerializer.Deserialize<string[]>(template.RequiredMetadataJson) ?? [];
        var builder = new StringBuilder();
        builder.AppendLine("Field,Value,Example / guidance");
        foreach (var field in fields)
        {
            var copy = FieldCopy.GetValueOrDefault(
                field,
                (Label: Humanize(field), Example: "Enter a value")
            );
            builder.Append(Csv(copy.Label)).Append(',').Append(',').AppendLine(Csv(copy.Example));
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    public static byte[] Report(
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows
    )
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Податоци");
        sheet.ShowGridLines = false;
        sheet.Range(1, 1, 1, headers.Count).Merge();
        sheet.Cell(1, 1).Value = title;
        sheet
            .Range(1, 1, 1, headers.Count)
            .Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FFF3F4"))
            .Font.SetFontColor(XLColor.FromHtml("#8F0F1B"))
            .Font.SetBold()
            .Font.SetFontSize(18);
        sheet.Row(1).Height = 34;
        sheet.Cell(2, 1).Value = $"Извезено: {DateTimeOffset.Now:dd.MM.yyyy HH:mm}";
        sheet.Range(2, 1, 2, headers.Count).Merge();
        sheet.Range(2, 1, 2, headers.Count).Style.Font.SetFontColor(XLColor.FromHtml("#66757B"));

        const int headerRow = 4;
        for (var column = 0; column < headers.Count; column++)
            sheet.Cell(headerRow, column + 1).Value = headers[column];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        for (var column = 0; column < headers.Count; column++)
            sheet.Cell(headerRow + rowIndex + 1, column + 1).Value =
                column < rows[rowIndex].Count ? rows[rowIndex][column] ?? "" : "";

        var lastRow = Math.Max(headerRow + rows.Count, headerRow + 1);
        sheet
            .Range(headerRow, 1, headerRow, headers.Count)
            .Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F1F3F4"))
            .Font.SetFontColor(XLColor.FromHtml("#172126"))
            .Font.SetBold();
        sheet.Range(headerRow, 1, lastRow, headers.Count).Style.Border.BottomBorder =
            XLBorderStyleValues.Thin;
        sheet.Range(headerRow, 1, lastRow, headers.Count).Style.Border.BottomBorderColor =
            XLColor.FromHtml("#D9DEE2");
        sheet.Range(headerRow, 1, lastRow, headers.Count).Style.Alignment.WrapText = true;
        sheet.Columns(1, headers.Count).AdjustToContents(12, 45);
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Range(headerRow, 1, lastRow, headers.Count).SetAutoFilter();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string Humanize(string value) =>
        string.Concat(
            value.Select(
                (character, index) =>
                    index > 0 && char.IsUpper(character)
                        ? $" {char.ToLowerInvariant(character)}"
                        : character.ToString()
            )
        );
}
