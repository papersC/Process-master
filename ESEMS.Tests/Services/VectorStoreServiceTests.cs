using ESEMS.Web.Services.AI;

namespace ESEMS.Tests.Services;

public class VectorStoreServiceTests
{
    [Fact]
    public void Tokenize_LowercasesAndSplits_OnPunctuation()
    {
        var tokens = VectorStoreService.Tokenize("Housing LOAN, Purchase. Emirates-ID!");
        Assert.Contains("housing", tokens);
        Assert.Contains("loan", tokens);
        Assert.Contains("purchase", tokens);
    }

    [Fact]
    public void Tokenize_RemovesStopWords()
    {
        var tokens = VectorStoreService.Tokenize("the process of the approval is the key");
        Assert.DoesNotContain("the", tokens);
        Assert.DoesNotContain("of", tokens);
        Assert.DoesNotContain("is", tokens);
        Assert.Contains("process", tokens);
        Assert.Contains("approval", tokens);
    }

    [Fact]
    public void Tokenize_RemovesSingleCharacterTokens()
    {
        var tokens = VectorStoreService.Tokenize("a b c housing");
        Assert.DoesNotContain("a", tokens);
        Assert.DoesNotContain("b", tokens);
        Assert.DoesNotContain("c", tokens);
        Assert.Contains("housing", tokens);
    }

    [Fact]
    public void Tokenize_Returns_EmptySet_OnBlankInput()
    {
        Assert.Empty(VectorStoreService.Tokenize(""));
        Assert.Empty(VectorStoreService.Tokenize("   "));
        Assert.Empty(VectorStoreService.Tokenize(null!));
    }

    [Fact]
    public void CalculateScore_CountsExactMatches()
    {
        var q = new HashSet<string> { "housing", "loan", "approval" };
        var d = new HashSet<string> { "housing", "loan", "benefits" };
        Assert.Equal(2, VectorStoreService.CalculateScore(q, d));
    }

    [Fact]
    public void CalculateScore_MatchesPartialContainment()
    {
        // 'process' should match 'processing' via the Contains check on both sides.
        var q = new HashSet<string> { "process" };
        var d = new HashSet<string> { "processing" };
        Assert.Equal(1, VectorStoreService.CalculateScore(q, d));
    }

    [Fact]
    public void CalculateScore_ZeroWhenNoMatch()
    {
        var q = new HashSet<string> { "procurement", "sourcing" };
        var d = new HashSet<string> { "housing", "loan" };
        Assert.Equal(0, VectorStoreService.CalculateScore(q, d));
    }

    [Fact]
    public void CalculateScore_EmptyQuery_ReturnsZero()
    {
        Assert.Equal(0, VectorStoreService.CalculateScore(new HashSet<string>(), new HashSet<string> { "anything" }));
    }
}
