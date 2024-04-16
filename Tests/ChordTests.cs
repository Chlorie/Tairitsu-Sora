using TairitsuSora.Commands.Music;

namespace TairitsuSora.Tests;

[TestClass]
public class ChordTests
{
    [TestMethod]
    public void SuspendedChordDifferentSpelling()
    {
        Chord target = Chord.ParseFrom("Bbsus/F");
        Chord source = Chord.ParseFrom("Ebsus2");
        source.BassNote = target.BassNote;
        Assert.AreEqual(target, source);
    }
}
