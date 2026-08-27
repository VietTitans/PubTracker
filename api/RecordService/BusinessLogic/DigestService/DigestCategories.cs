namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Canonical body-part categories used in digest messages, shared across source
/// providers so a PEDro search and a PubMed search covering the same anatomical
/// region are reported under the same label. Originates from PEDro's own body_part
/// controlled vocabulary (search.pedro.org.au advanced search); PubMed has no such
/// fixed taxonomy of its own, so its searches are mapped onto these categories on
/// a best-effort basis (see PubMedDigestMessageBuilder).
/// </summary>
public static class DigestCategories
{
    public const string HeadOrNeck = "Head or neck";
    public const string UpperArmShoulderOrShoulderGirdle = "Upper arm, shoulder or shoulder girdle";
    public const string ForearmOrElbow = "Forearm or elbow";
    public const string HandOrWrist = "Hand or wrist";
    public const string Chest = "Chest (cardiothoracic)";
    public const string ThoracicSpine = "Thoracic spine";
    public const string LumbarSpineSijOrPelvis = "Lumbar spine, SIJ or pelvis";
    public const string PerineumOrGenitoUrinarySystem = "Perineum or genito-urinary system";
    public const string ThighOrHip = "Thigh or hip";
    public const string LowerLegOrKnee = "Lower leg or knee";
    public const string FootOrAnkle = "Foot or ankle";
    public const string WholeBodyOrNoSpecificBodyPart = "Whole body or no specific body part";
}
