namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Best-effort anatomical-category match against free text, using PEDro's own body-part
/// vocabulary (DigestCategories) as the shared taxonomy. PubMedDigestMessageBuilder relies on
/// this entirely, since PubMed has no structured body-part field of its own - the whole search
/// lives in free text. PedroDigestMessageBuilder falls back to it too, for a PEDro search that
/// leaves body_part unset ("Any/all") but still has a free-text term to match against, so a
/// PEDro digest doesn't fall back to "your search" just because the structured field was
/// skipped.
/// </summary>
internal static class CategoryKeywordMatcher
{
    private static readonly (string Keyword, string Category)[] KeywordMap =
    {
        ("shoulder", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("rotator cuff", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("humerus", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("scapula", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("clavicle", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("arm", DigestCategories.UpperArmShoulderOrShoulderGirdle),

        ("forearm", DigestCategories.ForearmOrElbow),
        ("elbow", DigestCategories.ForearmOrElbow),
        ("radius", DigestCategories.ForearmOrElbow),
        ("ulna", DigestCategories.ForearmOrElbow),

        ("wrist", DigestCategories.HandOrWrist),
        ("hand", DigestCategories.HandOrWrist),
        ("finger", DigestCategories.HandOrWrist),
        ("thumb", DigestCategories.HandOrWrist),
        ("carpal", DigestCategories.HandOrWrist),

        ("head", DigestCategories.HeadOrNeck),
        ("neck", DigestCategories.HeadOrNeck),
        ("cervical", DigestCategories.HeadOrNeck),
        ("face", DigestCategories.HeadOrNeck),
        ("skull", DigestCategories.HeadOrNeck),
        ("temporomandibular", DigestCategories.HeadOrNeck),

        ("thorax", DigestCategories.Chest),
        ("thoracic wall", DigestCategories.Chest),
        ("chest", DigestCategories.Chest),
        ("rib", DigestCategories.Chest),

        ("thoracic vertebra", DigestCategories.ThoracicSpine),
        ("thoracic spine", DigestCategories.ThoracicSpine),

        ("lumbar", DigestCategories.LumbarSpineSijOrPelvis),
        ("sacroiliac", DigestCategories.LumbarSpineSijOrPelvis),
        ("pelvis", DigestCategories.LumbarSpineSijOrPelvis),
        ("sacrum", DigestCategories.LumbarSpineSijOrPelvis),
        ("low back", DigestCategories.LumbarSpineSijOrPelvis),

        ("perineum", DigestCategories.PerineumOrGenitoUrinarySystem),
        ("urogenital", DigestCategories.PerineumOrGenitoUrinarySystem),
        ("pelvic floor", DigestCategories.PerineumOrGenitoUrinarySystem),

        ("thigh", DigestCategories.ThighOrHip),
        ("hip", DigestCategories.ThighOrHip),
        ("femur", DigestCategories.ThighOrHip),

        ("knee", DigestCategories.LowerLegOrKnee),
        ("leg", DigestCategories.LowerLegOrKnee),
        ("tibia", DigestCategories.LowerLegOrKnee),
        ("fibula", DigestCategories.LowerLegOrKnee),

        ("ankle", DigestCategories.FootOrAnkle),
        ("foot", DigestCategories.FootOrAnkle),
        ("toe", DigestCategories.FootOrAnkle),
    };

    public static string? Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lowerText = text.ToLowerInvariant();
        foreach (var (keyword, category) in KeywordMap)
        {
            if (lowerText.Contains(keyword))
            {
                return category;
            }
        }

        return null;
    }
}
