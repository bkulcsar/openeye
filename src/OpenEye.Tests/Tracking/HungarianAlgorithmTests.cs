using OpenEye.PipelineCore.Tracking;

namespace OpenEye.Tests.Tracking;

public class HungarianAlgorithmTests
{
    [Fact]
    public void Solve_1x1_ReturnsZero()
    {
        var cost = new double[,] { { 5.0 } };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal([0], result);
    }

    [Fact]
    public void Solve_2x2_ReturnsOptimalAssignment()
    {
        // Optimal: row 0→col 1 (cost 2), row 1→col 0 (cost 3) = total 5
        var cost = new double[,]
        {
            { 10, 2 },
            { 3, 10 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(1, result[0]);
        Assert.Equal(0, result[1]);
    }

    [Fact]
    public void Solve_3x3_ReturnsOptimalAssignment()
    {
        var cost = new double[,]
        {
            { 1, 2, 3 },
            { 2, 4, 6 },
            { 3, 6, 9 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        var cols = new HashSet<int>(result);
        Assert.Equal(3, cols.Count);
        double total = 0;
        for (int i = 0; i < 3; i++) total += cost[i, result[i]];
        Assert.True(total <= 14);
    }

    [Fact]
    public void Solve_MoreRowsThanCols_UnassignsExtraRows()
    {
        var cost = new double[,]
        {
            { 1, 5 },
            { 5, 1 },
            { 3, 3 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(3, result.Length);
        Assert.Contains(-1, result);
    }

    [Fact]
    public void Solve_MoreColsThanRows_AllRowsAssigned()
    {
        var cost = new double[,]
        {
            { 1, 5, 9 },
            { 5, 1, 9 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(2, result.Length);
        Assert.DoesNotContain(-1, result);
    }

    [Fact]
    public void Solve_EmptyMatrix_ReturnsEmpty()
    {
        var cost = new double[0, 0];
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Empty(result);
    }
}
