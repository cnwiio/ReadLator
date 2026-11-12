using System.Collections.Generic;
using NovelTranslator.Core.Models;
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
}
