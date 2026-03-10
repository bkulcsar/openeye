namespace OpenEye.PipelineCore.Tracking;

/// <summary>
/// Solves the linear assignment problem using the Hungarian (Munkres) algorithm.
/// O(n^3) time complexity.
/// </summary>
public static class HungarianAlgorithm
{
    /// <summary>
    /// Returns optimal column assignment for each row. -1 = unassigned.
    /// </summary>
    public static int[] Solve(double[,] costMatrix)
    {
        int rows = costMatrix.GetLength(0);
        int cols = costMatrix.GetLength(1);

        if (rows == 0 || cols == 0)
            return [];

        int n = Math.Max(rows, cols);
        var cost = new double[n, n];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                cost[i, j] = costMatrix[i, j];

        var u = new double[n + 1];
        var v = new double[n + 1];
        var p = new int[n + 1];
        var way = new int[n + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            var minv = new double[n + 1];
            var used = new bool[n + 1];
            Array.Fill(minv, double.MaxValue);

            do
            {
                used[j0] = true;
                int i0 = p[j0], j1 = 0;
                double delta = double.MaxValue;

                for (int j = 1; j <= n; j++)
                {
                    if (used[j]) continue;
                    double cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }
                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }

                for (int j = 0; j <= n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            }
            while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            }
            while (j0 != 0);
        }

        var result = new int[rows];
        Array.Fill(result, -1);
        for (int j = 1; j <= n; j++)
        {
            if (p[j] <= rows && j <= cols)
                result[p[j] - 1] = j - 1;
        }

        return result;
    }
}
