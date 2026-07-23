using System.Text.RegularExpressions;

namespace DigitMak.Portal.Api.Application;

public static partial class EmailTemplates
{
    private static readonly Dictionary<string, (string Subject, string Body)> Macedonian = new()
    {
        ["EmailVerification"] = (
            "Потврда на е-пошта",
            "<p>Потврдете ја вашата DigitMak сметка за да продолжите.</p>"
        ),
        ["PasswordReset"] = (
            "Промена на лозинка",
            "<p>Добивме барање за промена на лозинката на вашата DigitMak сметка.</p>"
        ),
        ["OrganizationSubmitted"] = (
            "Организацијата е испратена",
            "<p>Вашата организација е испратена на администраторска проверка.</p>"
        ),
        ["OrganizationApproved"] = (
            "Организацијата е одобрена",
            "<p>Вашата организација е одобрена во DigitMak порталот.</p>"
        ),
        ["OrganizationRejected"] = (
            "Организацијата е одбиена",
            "<p>Вашата организација не беше одобрена. Контактирајте го DigitMak тимот за дополнителни информации.</p>"
        ),
        ["OrganizationSuspended"] = (
            "Организацијата е суспендирана",
            "<p>Пристапот на организацијата е привремено суспендиран.</p>"
        ),
        ["OrganizationMembershipChanged"] = (
            "Промена на членството",
            "<p>Статусот на вашето членство во организацијата е променет.</p>"
        ),
        ["SubscriptionInvitation"] = (
            "Покана за претплата",
            "<p>Имате покана за лична годишна DigitMak претплата.</p>"
        ),
        ["SubscriptionActivated"] = (
            "Претплатата е активна",
            "<p>Вашата DigitMak претплата е активирана.</p>"
        ),
        ["SubscriptionExpiringSoon"] = (
            "Претплатата истекува наскоро",
            "<p>Вашата претплата истекува наскоро. Контактирајте го DigitMak тимот за продолжување.</p>"
        ),
        ["SubscriptionExpired"] = (
            "Претплатата е истечена",
            "<p>Вашата DigitMak претплата е истечена.</p>"
        ),
        ["SubscriptionCancelled"] = (
            "Претплатата е откажана",
            "<p>Вашата DigitMak претплата е откажана.</p>"
        ),
        ["ContactRequestConfirmation"] = (
            "Контакт барањето е примено",
            "<p>Го примивме вашето контакт барање. Тимот ќе одговори во рок од два работни дена.</p>"
        ),
        ["ContactRequestResponse"] = (
            "Одговор од DigitMak",
            "<p>Тимот на DigitMak испрати одговор на вашето барање:</p>"
        ),
        ["RegistrationInvitation"] = (
            "Покана за регистрација",
            "<p>Креирајте DigitMak сметка за да продолжите со услугата.</p>"
        ),
        ["TicketCreated"] = (
            "Тикетот е креиран",
            "<p>Вашиот тикет е успешно креиран во DigitMak порталот.</p>"
        ),
        ["TicketAssigned"] = (
            "Тикетот е доделен",
            "<p>Вашиот тикет е доделен на член од DigitMak тимот.</p>"
        ),
        ["TicketStatusChanged"] = (
            "Променет статус на тикет",
            "<p>Статусот на вашиот тикет е променет. Отворете го порталот за детали.</p>"
        ),
        ["TicketMessageCreated"] = (
            "Нова порака во тикет",
            "<p>Имате нова порака во вашиот DigitMak тикет.</p>"
        ),
        ["TicketResolved"] = (
            "Тикетот е решен",
            "<p>Тикетот е означен како решен. Прегледајте ја финалната препорака во порталот.</p>"
        ),
        ["MeetingRequested"] = (
            "Побаран е состанок",
            "<p>Вашето барање за состанок е примено.</p>"
        ),
        ["MeetingConfirmed"] = (
            "Состанокот е потврден",
            "<p>Вашиот DigitMak состанок е потврден. Деталите се достапни во порталот.</p>"
        ),
        ["MeetingRejected"] = (
            "Состанокот е одбиен",
            "<p>Барањето за состанок не е потврдено. Отворете го порталот за детали.</p>"
        ),
        ["MeetingCancelled"] = ("Состанокот е откажан", "<p>Состанокот е откажан.</p>"),
        ["MeetingCompleted"] = (
            "Состанокот е завршен",
            "<p>Состанокот е означен како завршен.</p>"
        ),
    };

    private static readonly Dictionary<string, (string Subject, string Body)> Albanian = new()
    {
        ["EmailVerification"] = (
            "Konfirmimi i email-it",
            "<p>Konfirmoni llogarinë tuaj DigitMak për të vazhduar.</p>"
        ),
        ["PasswordReset"] = (
            "Ndryshimi i fjalëkalimit",
            "<p>Pranuam një kërkesë për ndryshimin e fjalëkalimit të llogarisë suaj.</p>"
        ),
        ["OrganizationSubmitted"] = (
            "Organizata u dorëzua",
            "<p>Organizata juaj u dërgua për verifikim administrativ.</p>"
        ),
        ["OrganizationApproved"] = (
            "Organizata u miratua",
            "<p>Organizata juaj u miratua në portalin DigitMak.</p>"
        ),
        ["OrganizationRejected"] = (
            "Organizata u refuzua",
            "<p>Organizata nuk u miratua. Kontaktoni ekipin DigitMak për më shumë informacion.</p>"
        ),
        ["OrganizationSuspended"] = (
            "Organizata u pezullua",
            "<p>Qasja e organizatës është pezulluar përkohësisht.</p>"
        ),
        ["OrganizationMembershipChanged"] = (
            "Ndryshim i anëtarësimit",
            "<p>Statusi i anëtarësimit tuaj në organizatë ka ndryshuar.</p>"
        ),
        ["SubscriptionInvitation"] = (
            "Ftesë për abonim",
            "<p>Keni një ftesë për abonim personal vjetor në DigitMak.</p>"
        ),
        ["SubscriptionActivated"] = (
            "Abonimi është aktiv",
            "<p>Abonimi juaj DigitMak është aktivizuar.</p>"
        ),
        ["SubscriptionExpiringSoon"] = (
            "Abonimi skadon së shpejti",
            "<p>Abonimi juaj skadon së shpejti. Kontaktoni ekipin DigitMak për vazhdim.</p>"
        ),
        ["SubscriptionExpired"] = (
            "Abonimi ka skaduar",
            "<p>Abonimi juaj DigitMak ka skaduar.</p>"
        ),
        ["SubscriptionCancelled"] = (
            "Abonimi u anulua",
            "<p>Abonimi juaj DigitMak është anuluar.</p>"
        ),
        ["ContactRequestConfirmation"] = (
            "Kërkesa u pranua",
            "<p>E pranuam kërkesën tuaj. Ekipi do të përgjigjet brenda dy ditësh pune.</p>"
        ),
        ["ContactRequestResponse"] = (
            "Përgjigje nga DigitMak",
            "<p>Ekipi DigitMak dërgoi përgjigje për kërkesën tuaj:</p>"
        ),
        ["RegistrationInvitation"] = (
            "Ftesë për regjistrim",
            "<p>Krijoni llogari DigitMak për të vazhduar me shërbimin.</p>"
        ),
        ["TicketCreated"] = (
            "Tiketa u krijua",
            "<p>Tiketa juaj u krijua me sukses në portalin DigitMak.</p>"
        ),
        ["TicketAssigned"] = (
            "Tiketa u caktua",
            "<p>Tiketa juaj iu caktua një anëtari të ekipit DigitMak.</p>"
        ),
        ["TicketStatusChanged"] = (
            "Statusi i tiketës ndryshoi",
            "<p>Statusi i tiketës suaj ka ndryshuar. Hapni portalin për hollësi.</p>"
        ),
        ["TicketMessageCreated"] = (
            "Mesazh i ri në tiketë",
            "<p>Keni një mesazh të ri në tiketën tuaj DigitMak.</p>"
        ),
        ["TicketResolved"] = (
            "Tiketa u zgjidh",
            "<p>Tiketa është shënuar si e zgjidhur. Shikoni rekomandimin përfundimtar në portal.</p>"
        ),
        ["MeetingRequested"] = ("Takimi u kërkua", "<p>Kërkesa juaj për takim u pranua.</p>"),
        ["MeetingConfirmed"] = (
            "Takimi u konfirmua",
            "<p>Takimi juaj DigitMak u konfirmua. Hollësitë janë në portal.</p>"
        ),
        ["MeetingRejected"] = (
            "Takimi u refuzua",
            "<p>Kërkesa për takim nuk u konfirmua. Hapni portalin për hollësi.</p>"
        ),
        ["MeetingCancelled"] = ("Takimi u anulua", "<p>Takimi është anuluar.</p>"),
        ["MeetingCompleted"] = ("Takimi përfundoi", "<p>Takimi është shënuar si i përfunduar.</p>"),
    };

    public static (string Subject, string Body) Render(
        string type,
        string? language,
        string originalSubject,
        string originalBody
    )
    {
        var locale = language is "en" or "sq" ? language : "mk";
        if (locale == "en")
            return (
                $"DigitMak - {originalSubject}",
                $"<p>DigitMak notification: <strong>{originalSubject}</strong></p>{originalBody}"
            );
        var templates = locale == "sq" ? Albanian : Macedonian;
        if (!templates.TryGetValue(type, out var template))
            return ($"DigitMak - {originalSubject}", originalBody);
        var body = template.Body;
        var link = LinkRegex().Match(originalBody);
        if (link.Success)
        {
            var action = locale == "sq" ? "Hap lidhjen e sigurt" : "Отвори ја безбедната врска";
            body += $"<p><a href=\"{link.Groups[1].Value}\">{action}</a></p>";
        }
        if (type == "ContactRequestResponse")
            body += originalBody;
        return (
            $"DigitMak - {template.Subject}",
            $"<p><strong>{template.Subject}</strong></p>{body}"
        );
    }

    [GeneratedRegex("href=[\\\"']([^\\\"']+)[\\\"']", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();
}
