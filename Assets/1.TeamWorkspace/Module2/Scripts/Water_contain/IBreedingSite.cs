/// <summary>
/// A breeding site that can be shown already dealt with, without the player having done it and without
/// awarding anything. M2Manager uses this for the sites a round leaves out, so the house keeps its
/// furniture: a jar with its lid on, a vase with no water, a fish bowl with fish in it.
/// </summary>
public interface IBreedingSite
{
    void ShowResolved();
}
