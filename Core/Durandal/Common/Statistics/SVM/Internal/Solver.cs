﻿using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Statistics.SVM
{
    // An SMO algorithm in Fan et al., JMLR 6(2005), p. 1889--1918
    // Solves:
    //
    //	min 0.5(\alpha^T Q \alpha) + p^T \alpha
    //
    //		y^T \alpha = \delta
    //		y_i = +1 or -1
    //		0 <= alpha_i <= Cp for y_i = 1
    //		0 <= alpha_i <= Cn for y_i = -1
    //
    // Given:
    //
    //	Q, p, y, Cp, Cn, and an initial feasible point \alpha
    //	l is the size of vectors and matrices
    //	eps is the stopping tolerance
    //
    // solution will be put in \alpha, objective value will be put in obj
    //
    public class Solver
    {
        protected int active_size;
        protected int[] y;
        protected double[] G;     // gradient of objective function
        public const int LOWER_BOUND = 0;
        public const int UPPER_BOUND = 1;
        static readonly int FREE = 2;
        int[] alpha_status;    // LOWER_BOUND, UPPER_BOUND, FREE
        double[] alpha;
        protected QMatrix Q;
        protected double[] QD;
        protected double eps;
        double Cp, Cn;
        double[] p;
        int[] active_set;
        double[] G_bar;     // gradient, if we treat free variables as 0
        protected int l;
        protected bool unshrink;   // XXX

        public const double INF = double.PositiveInfinity;

        private ILogger _logger = NullLogger.Singleton;

        double get_C(int i)
        {
            return (y[i] > 0) ? Cp : Cn;
        }

        void update_alpha_status(int i)
        {
            if (alpha[i] >= get_C(i))
                alpha_status[i] = UPPER_BOUND;
            else if (alpha[i] <= 0)
                alpha_status[i] = LOWER_BOUND;
            else alpha_status[i] = FREE;
        }

        protected bool is_upper_bound(int i) { return alpha_status[i] == UPPER_BOUND; }
        protected bool is_lower_bound(int i) { return alpha_status[i] == LOWER_BOUND; }
        bool is_free(int i) { return alpha_status[i] == FREE; }

        // java: information about solution except alpha,
        // because we cannot return multiple values otherwise...
        public class SolutionInfo
        {
            public double Obj { get; set; }
            public double Rho { get; set; }
            public double UpperBoundP { get; set; }
            public double UpperBoundN { get; set; }
            public double r { get; set; }   // for Solver_NU
        }

        protected void swap_index(int i, int j)
        {
            Q.swap_index(i, j);
            do { int tmp = y[i]; y[i] = y[j]; y[j] = tmp; } while (false);
            do { double tmp = G[i]; G[i] = G[j]; G[j] = tmp; } while (false);
            do { int tmp = alpha_status[i]; alpha_status[i] = alpha_status[j]; alpha_status[j] = tmp; } while (false);
            do { double tmp = alpha[i]; alpha[i] = alpha[j]; alpha[j] = tmp; } while (false);
            do { double tmp = p[i]; p[i] = p[j]; p[j] = tmp; } while (false);
            do { int tmp = active_set[i]; active_set[i] = active_set[j]; active_set[j] = tmp; } while (false);
            do { double tmp = G_bar[i]; G_bar[i] = G_bar[j]; G_bar[j] = tmp; } while (false);
        }

        protected void reconstruct_gradient()
        {
            // reconstruct inactive elements of G from G_bar and free variables

            if (active_size == l) return;

            int i, j;
            int nr_free = 0;

            for (j = active_size; j < l; j++)
                G[j] = G_bar[j] + p[j];

            for (j = 0; j < active_size; j++)
                if (is_free(j))
                    nr_free++;

            if (2 * nr_free < active_size)
                _logger.Log("Using -h 0 may be faster", LogLevel.Wrn);

            if (nr_free * l > 2 * active_size * (l - active_size))
            {
                for (i = active_size; i < l; i++)
                {
                    float[] Q_i = Q.get_Q(i, active_size);
                    for (j = 0; j < active_size; j++)
                        if (is_free(j))
                            G[i] += alpha[j] * Q_i[j];
                }
            }
            else
            {
                for (i = 0; i < active_size; i++)
                    if (is_free(i))
                    {
                        float[] Q_i = Q.get_Q(i, l);
                        double alpha_i = alpha[i];
                        for (j = active_size; j < l; j++)
                            G[j] += alpha_i * Q_i[j];
                    }
            }
        }

        public virtual void Solve(int l, QMatrix Q, double[] p_, int[] y_,
               double[] alpha_, double Cp, double Cn, double eps, SolutionInfo si, bool shrinking)
        {
            this.l = l;
            this.Q = Q;
            QD = Q.get_QD();
            p = (double[]) p_.Clone();
            y = (int[]) y_.Clone();
            alpha = (double[]) alpha_.Clone();
            this.Cp = Cp;
            this.Cn = Cn;
            this.eps = eps;
            this.unshrink = false;

            // initialize alpha_status
            {
                alpha_status = new int[l];
                for (int i = 0; i < l; i++)
                    update_alpha_status(i);
            }

            // initialize active set (for shrinking)
            {
                active_set = new int[l];
                for (int i = 0; i < l; i++)
                    active_set[i] = i;
                active_size = l;
            }

            // initialize gradient
            {
                G = new double[l];
                G_bar = new double[l];
                int i;
                for (i = 0; i < l; i++)
                {
                    G[i] = p[i];
                    G_bar[i] = 0;
                }
                for (i = 0; i < l; i++)
                    if (!is_lower_bound(i))
                    {
                        float[] Q_i = Q.get_Q(i, l);
                        double alpha_i = alpha[i];
                        int j;
                        for (j = 0; j < l; j++)
                            G[j] += alpha_i * Q_i[j];
                        if (is_upper_bound(i))
                            for (j = 0; j < l; j++)
                                G_bar[j] += get_C(i) * Q_i[j];
                    }
            }

            // optimization step

            int iter = 0;
            int max_iter = Math.Max(10000000, l > int.MaxValue / 100 ? int.MaxValue : 100 * l);
            int counter = Math.Min(l, 1000) + 1;
            int[] working_set = new int[2];

            while (iter < max_iter)
            {
                // show progress and do shrinking

                if (--counter == 0)
                {
                    counter = Math.Min(l, 1000);
                    if (shrinking) do_shrinking();
                }

                if (select_working_set(working_set) != 0)
                {
                    // reconstruct the whole gradient
                    reconstruct_gradient();
                    // reset active set size and check
                    active_size = l;
                    if (select_working_set(working_set) != 0)
                        break;
                    else
                        counter = 1;    // do shrinking next iteration
                }

                int i = working_set[0];
                int j = working_set[1];

                ++iter;

                // update alpha[i] and alpha[j], handle bounds carefully

                float[] Q_i = Q.get_Q(i, active_size);
                float[] Q_j = Q.get_Q(j, active_size);

                double C_i = get_C(i);
                double C_j = get_C(j);

                double old_alpha_i = alpha[i];
                double old_alpha_j = alpha[j];

                if (y[i] != y[j])
                {
                    double quad_coef = QD[i] + QD[j] + 2 * Q_i[j];
                    if (quad_coef <= 0)
                        quad_coef = 1e-12;
                    double delta = (-G[i] - G[j]) / quad_coef;
                    double diff = alpha[i] - alpha[j];
                    alpha[i] += delta;
                    alpha[j] += delta;

                    if (diff > 0)
                    {
                        if (alpha[j] < 0)
                        {
                            alpha[j] = 0;
                            alpha[i] = diff;
                        }
                    }
                    else
                    {
                        if (alpha[i] < 0)
                        {
                            alpha[i] = 0;
                            alpha[j] = -diff;
                        }
                    }
                    if (diff > C_i - C_j)
                    {
                        if (alpha[i] > C_i)
                        {
                            alpha[i] = C_i;
                            alpha[j] = C_i - diff;
                        }
                    }
                    else
                    {
                        if (alpha[j] > C_j)
                        {
                            alpha[j] = C_j;
                            alpha[i] = C_j + diff;
                        }
                    }
                }
                else
                {
                    double quad_coef = QD[i] + QD[j] - 2 * Q_i[j];
                    if (quad_coef <= 0)
                        quad_coef = 1e-12;
                    double delta = (G[i] - G[j]) / quad_coef;
                    double sum = alpha[i] + alpha[j];
                    alpha[i] -= delta;
                    alpha[j] += delta;

                    if (sum > C_i)
                    {
                        if (alpha[i] > C_i)
                        {
                            alpha[i] = C_i;
                            alpha[j] = sum - C_i;
                        }
                    }
                    else
                    {
                        if (alpha[j] < 0)
                        {
                            alpha[j] = 0;
                            alpha[i] = sum;
                        }
                    }
                    if (sum > C_j)
                    {
                        if (alpha[j] > C_j)
                        {
                            alpha[j] = C_j;
                            alpha[i] = sum - C_j;
                        }
                    }
                    else
                    {
                        if (alpha[i] < 0)
                        {
                            alpha[i] = 0;
                            alpha[j] = sum;
                        }
                    }
                }

                // update G

                double delta_alpha_i = alpha[i] - old_alpha_i;
                double delta_alpha_j = alpha[j] - old_alpha_j;

                for (int k = 0; k < active_size; k++)
                {
                    G[k] += Q_i[k] * delta_alpha_i + Q_j[k] * delta_alpha_j;
                }

                // update alpha_status and G_bar

                {
                    bool ui = is_upper_bound(i);
                    bool uj = is_upper_bound(j);
                    update_alpha_status(i);
                    update_alpha_status(j);
                    int k;
                    if (ui != is_upper_bound(i))
                    {
                        Q_i = Q.get_Q(i, l);
                        if (ui)
                            for (k = 0; k < l; k++)
                                G_bar[k] -= C_i * Q_i[k];
                        else
                            for (k = 0; k < l; k++)
                                G_bar[k] += C_i * Q_i[k];
                    }

                    if (uj != is_upper_bound(j))
                    {
                        Q_j = Q.get_Q(j, l);
                        if (uj)
                            for (k = 0; k < l; k++)
                                G_bar[k] -= C_j * Q_j[k];
                        else
                            for (k = 0; k < l; k++)
                                G_bar[k] += C_j * Q_j[k];
                    }
                }

            }

            if (iter >= max_iter)
            {
                if (active_size < l)
                {
                    // reconstruct the whole gradient to calculate objective value
                    reconstruct_gradient();
                    active_size = l;
                }
                _logger.Log("Reaching max number of iterations", LogLevel.Wrn);
            }

            // calculate rho

            si.Rho = calculate_rho();

            // calculate objective value
            {
                double v = 0;
                int i;
                for (i = 0; i < l; i++)
                    v += alpha[i] * (G[i] + p[i]);

                si.Obj = v / 2;
            }

            // put back the solution
            {
                for (int i = 0; i < l; i++)
                    alpha_[active_set[i]] = alpha[i];
            }

            si.UpperBoundP = Cp;
            si.UpperBoundN = Cn;

            _logger.Log("Optimization finished, #iter = " + iter , LogLevel.Std);
        }

        // return 1 if already optimal, return 0 otherwise
        int select_working_set(int[] working_set)
        {
            // return i,j such that
            // i: maximizes -y_i * grad(f)_i, i in I_up(\alpha)
            // j: mimimizes the decrease of obj value
            //    (if quadratic coefficeint <= 0, replace it with tau)
            //    -y_j*grad(f)_j < -y_i*grad(f)_i, j in I_low(\alpha)

            double Gmax = -INF;
            double Gmax2 = -INF;
            int Gmax_idx = -1;
            int Gmin_idx = -1;
            double obj_diff_min = INF;

            for (int t = 0; t < active_size; t++)
                if (y[t] == +1)
                {
                    if (!is_upper_bound(t))
                        if (-G[t] >= Gmax)
                        {
                            Gmax = -G[t];
                            Gmax_idx = t;
                        }
                }
                else
                {
                    if (!is_lower_bound(t))
                        if (G[t] >= Gmax)
                        {
                            Gmax = G[t];
                            Gmax_idx = t;
                        }
                }

            int i = Gmax_idx;
            float[] Q_i = null;
            if (i != -1) // null Q_i not accessed: Gmax=-INF if i=-1
                Q_i = Q.get_Q(i, active_size);

            for (int j = 0; j < active_size; j++)
            {
                if (y[j] == +1)
                {
                    if (!is_lower_bound(j))
                    {
                        double grad_diff = Gmax + G[j];
                        if (G[j] >= Gmax2)
                            Gmax2 = G[j];
                        if (grad_diff > 0)
                        {
                            double obj_diff;
                            double quad_coef = QD[i] + QD[j] - 2.0 * y[i] * Q_i[j];
                            if (quad_coef > 0)
                                obj_diff = -(grad_diff * grad_diff) / quad_coef;
                            else
                                obj_diff = -(grad_diff * grad_diff) / 1e-12;

                            if (obj_diff <= obj_diff_min)
                            {
                                Gmin_idx = j;
                                obj_diff_min = obj_diff;
                            }
                        }
                    }
                }
                else
                {
                    if (!is_upper_bound(j))
                    {
                        double grad_diff = Gmax - G[j];
                        if (-G[j] >= Gmax2)
                            Gmax2 = -G[j];
                        if (grad_diff > 0)
                        {
                            double obj_diff;
                            double quad_coef = QD[i] + QD[j] + 2.0 * y[i] * Q_i[j];
                            if (quad_coef > 0)
                                obj_diff = -(grad_diff * grad_diff) / quad_coef;
                            else
                                obj_diff = -(grad_diff * grad_diff) / 1e-12;

                            if (obj_diff <= obj_diff_min)
                            {
                                Gmin_idx = j;
                                obj_diff_min = obj_diff;
                            }
                        }
                    }
                }
            }

            if (Gmax + Gmax2 < eps)
                return 1;

            working_set[0] = Gmax_idx;
            working_set[1] = Gmin_idx;
            return 0;
        }

        private bool be_shrunk(int i, double Gmax1, double Gmax2)
        {
            if (is_upper_bound(i))
            {
                if (y[i] == +1)
                    return (-G[i] > Gmax1);
                else
                    return (-G[i] > Gmax2);
            }
            else if (is_lower_bound(i))
            {
                if (y[i] == +1)
                    return (G[i] > Gmax2);
                else
                    return (G[i] > Gmax1);
            }
            else
                return (false);
        }

        void do_shrinking()
        {
            int i;
            double Gmax1 = -INF;        // max { -y_i * grad(f)_i | i in I_up(\alpha) }
            double Gmax2 = -INF;        // max { y_i * grad(f)_i | i in I_low(\alpha) }

            // find maximal violating pair first
            for (i = 0; i < active_size; i++)
            {
                if (y[i] == +1)
                {
                    if (!is_upper_bound(i))
                    {
                        if (-G[i] >= Gmax1)
                            Gmax1 = -G[i];
                    }
                    if (!is_lower_bound(i))
                    {
                        if (G[i] >= Gmax2)
                            Gmax2 = G[i];
                    }
                }
                else
                {
                    if (!is_upper_bound(i))
                    {
                        if (-G[i] >= Gmax2)
                            Gmax2 = -G[i];
                    }
                    if (!is_lower_bound(i))
                    {
                        if (G[i] >= Gmax1)
                            Gmax1 = G[i];
                    }
                }
            }

            if (unshrink == false && Gmax1 + Gmax2 <= eps * 10)
            {
                unshrink = true;
                reconstruct_gradient();
                active_size = l;
            }

            for (i = 0; i < active_size; i++)
                if (be_shrunk(i, Gmax1, Gmax2))
                {
                    active_size--;
                    while (active_size > i)
                    {
                        if (!be_shrunk(active_size, Gmax1, Gmax2))
                        {
                            swap_index(i, active_size);
                            break;
                        }
                        active_size--;
                    }
                }
        }

        double calculate_rho()
        {
            double r;
            int nr_free = 0;
            double ub = INF, lb = -INF, sum_free = 0;
            for (int i = 0; i < active_size; i++)
            {
                double yG = y[i] * G[i];

                if (is_lower_bound(i))
                {
                    if (y[i] > 0)
                        ub = Math.Min(ub, yG);
                    else
                        lb = Math.Max(lb, yG);
                }
                else if (is_upper_bound(i))
                {
                    if (y[i] < 0)
                        ub = Math.Min(ub, yG);
                    else
                        lb = Math.Max(lb, yG);
                }
                else
                {
                    ++nr_free;
                    sum_free += yG;
                }
            }

            if (nr_free > 0)
                r = sum_free / nr_free;
            else
                r = (ub + lb) / 2;

            return r;
        }

    }
}
