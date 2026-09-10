using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Tests.Outlook;

[TestClass]
[TestCategory("Unit")]
public sealed class OutlookHtmlComposerTests
{
    [TestMethod]
    public void SanitizeTemplate_AllowedFormattingTableAndCidImage_PreservesSafeSubset()
    {
        const string template = """
            <p style="color:#224488;position:fixed"><strong>Accesos</strong></p>
            <table border="1" onclick="bad()"><tr><td colspan="2">{{REQUEST_IDS}}</td></tr></table>
            <img src="cid:image-1@pam" alt="Diagrama" onerror="bad()" />
            """;

        string result = OutlookHtmlComposer.SanitizeTemplate(template);

        Assert.Contains("<p style=\"color:#224488\">", result);
        Assert.Contains("<strong>Accesos</strong>", result);
        Assert.Contains("<table border=\"1\">", result);
        Assert.Contains("<td colspan=\"2\">{{REQUEST_IDS}}</td>", result);
        Assert.Contains("<img src=\"cid:image-1@pam\" alt=\"Diagrama\" />", result);
        Assert.DoesNotContain("onclick", result);
        Assert.DoesNotContain("onerror", result);
        Assert.DoesNotContain("position", result);
    }

    [TestMethod]
    public void SanitizeTemplate_ActiveContentAndExternalResources_RemovesUnsafeNodes()
    {
        const string template = """
            <p>Inicio</p><script><p>active</p></script><iframe src="https://example.test"></iframe>
            <img src="https://example.test/image.png" alt="remote" />
            <a href="https://example.test">Texto conservado</a><p>{{REQUEST_IDS}}</p>
            """;

        string result = OutlookHtmlComposer.SanitizeTemplate(template);

        Assert.DoesNotContain("script", result);
        Assert.DoesNotContain("active", result);
        Assert.DoesNotContain("iframe", result);
        Assert.DoesNotContain("https://", result);
        Assert.DoesNotContain("<img", result);
        Assert.DoesNotContain("<a", result);
        Assert.Contains("Texto conservado", result);
    }

    [TestMethod]
    public void SanitizeTemplate_CaseVariantDuplicateAttributes_KeepsOneDeterministicValue()
    {
        string result = OutlookHtmlComposer.SanitizeTemplate(
            "<p STYLE=\"color:red\" style=\"color:blue\">{{REQUEST_IDS}}</p>");

        Assert.AreEqual("<p style=\"color:red\">{{REQUEST_IDS}}</p>", result);
    }

    [TestMethod]
    public void Compose_ValidTemplate_InsertsUniqueNumericIdsOncePerLineInOrder()
    {
        const string template = "<table><tr><td>Solicitudes</td><td>{{REQUEST_IDS}}</td></tr></table>";

        string result = OutlookHtmlComposer.Compose(template, ["150901", "150902", "150903"]);

        Assert.AreEqual(
            "<table><tr><td>Solicitudes</td><td>150901<br />150902<br />150903</td></tr></table>",
            result);
        Assert.DoesNotContain(OutlookHtmlComposer.RequestIdsMarker, result);
    }

    [TestMethod]
    public void Compose_MarkerEmbeddedInText_PreservesSurroundingContent()
    {
        string result = OutlookHtmlComposer.Compose(
            "<p>IDs: {{REQUEST_IDS}}.</p>",
            ["150901", "150902"]);

        Assert.AreEqual("<p>IDs: 150901<br />150902.</p>", result);
    }

    [TestMethod]
    [DataRow(null, OutlookHtmlCompositionErrorCode.MissingTemplate)]
    [DataRow("", OutlookHtmlCompositionErrorCode.MissingTemplate)]
    [DataRow("<p>Sin marcador</p>", OutlookHtmlCompositionErrorCode.MissingRequestIdsMarker)]
    [DataRow("<p>{{REQUEST_IDS}} {{REQUEST_IDS}}</p>", OutlookHtmlCompositionErrorCode.MultipleRequestIdsMarkers)]
    [DataRow("<p>{{REQUEST_IDS}}</div>", OutlookHtmlCompositionErrorCode.InvalidMarkup)]
    [DataRow("<!DOCTYPE x [<!ENTITY y 'value'>]><p>{{REQUEST_IDS}}</p>", OutlookHtmlCompositionErrorCode.InvalidMarkup)]
    public void SanitizeTemplate_InvalidTemplate_ThrowsStableError(
        string? template,
        OutlookHtmlCompositionErrorCode expectedCode)
    {
        OutlookHtmlCompositionException exception = Assert.ThrowsExactly<OutlookHtmlCompositionException>(
            () => OutlookHtmlComposer.SanitizeTemplate(template));

        Assert.AreEqual(expectedCode, exception.ErrorCode);
    }

    [TestMethod]
    public void SanitizeTemplate_MarkerOnlyInAttribute_DoesNotTreatItAsContentMarker()
    {
        OutlookHtmlCompositionException exception = Assert.ThrowsExactly<OutlookHtmlCompositionException>(
            () => OutlookHtmlComposer.SanitizeTemplate("<p title=\"{{REQUEST_IDS}}\">Texto</p>"));

        Assert.AreEqual(OutlookHtmlCompositionErrorCode.MissingRequestIdsMarker, exception.ErrorCode);
    }

    [TestMethod]
    public void SanitizeTemplate_TemplateExceedsLimit_ThrowsBeforeParsing()
    {
        string template = new('a', 8_000_001);

        OutlookHtmlCompositionException exception = Assert.ThrowsExactly<OutlookHtmlCompositionException>(
            () => OutlookHtmlComposer.SanitizeTemplate(template));

        Assert.AreEqual(OutlookHtmlCompositionErrorCode.TemplateTooLarge, exception.ErrorCode);
    }

    [TestMethod]
    public void Compose_MissingInvalidOrDuplicateIds_ThrowsStableErrors()
    {
        const string template = "<p>{{REQUEST_IDS}}</p>";

        Assert.AreEqual(
            OutlookHtmlCompositionErrorCode.MissingRequestIds,
            Assert.ThrowsExactly<OutlookHtmlCompositionException>(
                () => OutlookHtmlComposer.Compose(template, null)).ErrorCode);
        Assert.AreEqual(
            OutlookHtmlCompositionErrorCode.InvalidRequestId,
            Assert.ThrowsExactly<OutlookHtmlCompositionException>(
                () => OutlookHtmlComposer.Compose(template, ["REQ-1"])).ErrorCode);
        Assert.AreEqual(
            OutlookHtmlCompositionErrorCode.DuplicateRequestId,
            Assert.ThrowsExactly<OutlookHtmlCompositionException>(
                () => OutlookHtmlComposer.Compose(template, ["150901", "150901"])).ErrorCode);
    }

    [TestMethod]
    public void Compose_DangerousMarkerIsRemoved_DoesNotComposeFromActiveContent()
    {
        OutlookHtmlCompositionException exception = Assert.ThrowsExactly<OutlookHtmlCompositionException>(
            () => OutlookHtmlComposer.Compose(
                "<script>{{REQUEST_IDS}}</script><p>Visible</p>",
                ["150901"]));

        Assert.AreEqual(OutlookHtmlCompositionErrorCode.MissingRequestIdsMarker, exception.ErrorCode);
    }

    [TestMethod]
    public void CompositionException_MessageIsStableAndSafe()
    {
        var exception = new OutlookHtmlCompositionException(
            OutlookHtmlCompositionErrorCode.InvalidMarkup);

        Assert.AreEqual("El contenido enriquecido del correo no tiene un formato válido.", exception.Message);
        Assert.DoesNotContain("private HTML", exception.Message);
        Assert.AreEqual(
            "No fue posible preparar el contenido del correo.",
            new OutlookHtmlCompositionException((OutlookHtmlCompositionErrorCode)999).Message);
    }
}
