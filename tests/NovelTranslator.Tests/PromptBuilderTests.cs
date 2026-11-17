using System.Collections.Generic;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;
using Xunit;

namespace NovelTranslator.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void DetectPovHint_FirstPersonInNarration_IdentifiesFirstPerson()
    {
        string text = @"I walked through the empty street, clutching my coat tightly.
""Where are you going?"" he asked from behind.
I did not answer him, knowing that my life depended on running faster. I felt my heart beating.";

        string hint = PromptBuilder.DetectPovHint(text);
        Assert.Contains("บุรุษที่ 1", hint);
    }

    [Fact]
    public void DetectPovHint_DialogueWithFirstPerson_DoesNotMisclassifyThirdPersonNarration()
    {
        string text = @"John entered the grand hall with caution. He scanned the dark corners of the room.
""I am the master of this domain! I will destroy you!"" shouted the villain.
He drew his sword, refusing to let the enemy intimidate him. He stepped forward.";

        string hint = PromptBuilder.DetectPovHint(text);
        Assert.Contains("บุรุษที่ 3", hint);
    }

    [Fact]
    public void BuildMessages_IncludesAntiTranslationeseAndLookAheadContext()
    {
        var story = new Story { Title = "Test Story", SourceLang = "English", TargetLang = "Thai", StyleNotes = "แนวแอ็กชัน" };
        var messages = PromptBuilder.BuildMessages(
            story,
            chunkText: "He drew his sword and struck.",
            matchedGlossary: new List<GlossaryEntry>(),
            previousSourceContext: "He looked at the beast.",
            previousTranslationContext: "เขามองดูอสูรร้าย",
            retryHint: null,
            customSystemPrompt: null,
            nextSourceContext: "The beast roared in pain.");

        Assert.Equal(2, messages.Count);
        // System prompt contains Anti-Translationese rules
        Assert.Contains("Anti-Translationese", messages[0].Content);
        Assert.Contains("Passive Voice", messages[0].Content);
        // User prompt contains Look-Ahead Context
        Assert.Contains("บริบทย่อหน้าถัดไป", messages[1].Content);
        Assert.Contains("The beast roared in pain.", messages[1].Content);
        Assert.Contains("บริบทย่อหน้าก่อนหน้า", messages[1].Content);
    }

    [Fact]
    public void BuildSystemPrompt_FullTemplateReplacesPlaceholdersAndAutoAppendsStyleNotesWhenOmitted()
    {
        var story = new Story
        {
            Title = "Template Story",
            SourceLang = "Japanese",
            TargetLang = "Thai",
            StyleNotes = "สำนวนไลท์โนเวล"
        };

        string withPlaceholders = PromptBuilder.BuildSystemPrompt(
            story,
            "Translate from {SourceLang} to {TargetLang}.\nKeep tone faithful.\n{StyleNotes}");

        Assert.Contains("Translate from Japanese to Thai.", withPlaceholders);
        Assert.Contains("คำแนะนำสไตล์และโทนเรื่อง: สำนวนไลท์โนเวล", withPlaceholders);
        Assert.DoesNotContain("{SourceLang}", withPlaceholders);
        Assert.DoesNotContain("{StyleNotes}", withPlaceholders);

        string omittedStyle = PromptBuilder.BuildSystemPrompt(
            story,
            "Translate {SourceLang} -> {TargetLang}.\nFollow glossary strictly.");

        Assert.Contains("Translate Japanese -> Thai.", omittedStyle);
        Assert.EndsWith("คำแนะนำสไตล์และโทนเรื่อง: สำนวนไลท์โนเวล", omittedStyle);
    }

    [Fact]
    public void ResolvePovHint_RespectsConfiguredPovMode()
    {
        string thirdPersonText = "He walked down the road. He drew his sword. He looked ahead.";

        Assert.Empty(PromptBuilder.ResolvePovHint(thirdPersonText, PovDetectionMode.Off));
        Assert.Contains("บุรุษที่ 1", PromptBuilder.ResolvePovHint(thirdPersonText, PovDetectionMode.FirstPerson));
        Assert.Contains("บุรุษที่ 3", PromptBuilder.ResolvePovHint("Short dialogue only.", PovDetectionMode.ThirdPerson));
        Assert.Contains("บุรุษที่ 3", PromptBuilder.ResolvePovHint(thirdPersonText, PovDetectionMode.Auto));
    }
}
