/*
 * Most of the code in this document was copied from https://github.com/mbreese/swalign and then modified.
 * Modifications inlcude simplificaiton of the code to remove unneeded funtionality, and converstion from Python to C#
 * The contents of the LICENSE file from https://github.com/mbreese/swalign are included at the bottom of this file.
 */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PairwiseAligner
{
    class ScoringMatrix
    {
        double match, mismatch, wildcardMatch;
        public ScoringMatrix(double match_in = 2, double mismatch_in = -1, double wildcardMatch_in = 0)
        {
            match = match_in;
            mismatch = mismatch_in;
            wildcardMatch = wildcardMatch_in;
        }

        public double score(char base1, char base2, string wildcard = "N")
        {
            if ((wildcard != "") & (wildcard.Contains(base1) | wildcard.Contains(base2)))
            {
                return wildcardMatch;
            }
            else if (base1 == base2)
            {
                return match;
            }
            else
            {
                return mismatch;
            }

        }
    }

    class AlignmentMatrix
    {
        public int rows, columns;
        Tuple<double, char, int>[,] tupleArray;
        Tuple<double, char, int> initTuple = new Tuple<double, char, int>(0, ' ', 0);
        public AlignmentMatrix(int rows_in, int columns_in, Tuple<double, char, int>? init = null)
        {
            rows = rows_in;
            columns = columns_in;
            if (init == null)
            {
                init = initTuple;
            }

            tupleArray = new Tuple<double, char, int>[rows, columns];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    tupleArray[i, j] = init;
                }
            }
        }

        public void SetValue(int row, int column, Tuple<double, char, int> val)
        {
            tupleArray[row, column] = val;
        }

        public Tuple<double, char, int> GetValue(int row, int column)
        {
            return tupleArray[row, column];
        }
    }

    public class PairwiseAligner
    {
        double match, mismatch, wildcardMatch, gap_penalty, gap_extension_penalty;
        bool prefer_gap_runs, globalalign, full_query;
        string wildcard;
        ScoringMatrix scoringMatrix;

        public PairwiseAligner(double match_in = 2,
            double mismatch_in = -1,
            double wildcardMatch_in = 0,
            double gap_penalty_in = -10,
            double gap_extension_penalty_in = -0.5,
            bool prefer_gap_runs_in = true,
            bool globalalign_in = false,
            bool full_query_in = false,
            string wildcard_in = "N")
        {
            match = match_in;
            mismatch = mismatch_in;
            wildcardMatch = wildcardMatch_in;
            gap_penalty = gap_penalty_in;
            gap_extension_penalty = gap_extension_penalty_in;
            prefer_gap_runs = prefer_gap_runs_in;
            globalalign = globalalign_in;
            full_query = full_query_in;
            wildcard = wildcard_in;

            scoringMatrix = new ScoringMatrix(match, mismatch, wildcardMatch);
        }

        public Alignment Align(string refSeq, string querySeq)
        {
            string origRef = refSeq;
            string origQuery = querySeq;

            refSeq = refSeq.ToUpper();
            querySeq = querySeq.ToUpper();

            AlignmentMatrix matrix = new AlignmentMatrix(querySeq.Length + 1, refSeq.Length + 1);

            Tuple<double, char, int> startTuple = new Tuple<double, char, int>(0, 'i', 0);
            for (int row = 0; row < matrix.rows; row++)
            {
                matrix.SetValue(row, 0, startTuple);
            }

            startTuple = new Tuple<double, char, int>(0, 'd', 0);
            for (int col = 0; col < matrix.columns; col++)
            {
                matrix.SetValue(0, col, startTuple);
            }

            double max_val = 0;
            int max_row = 0;
            int max_col = 0;

            for (int row = 1; row < matrix.rows; row++)
            {
                for (int col = 1; col < matrix.columns; col++)
                {
                    double mm_val = matrix.GetValue(row - 1, col - 1).Item1 + scoringMatrix.score(querySeq[row - 1], refSeq[col - 1], wildcard);
                    int ins_run = 0;
                    int del_run = 0;
                    double ins_val = 0;
                    double del_val = 0;
                    double cell_val = 0;
                    Tuple<double, char, int> tupValue;

                    if (matrix.GetValue(row - 1, col).Item2 == 'i')
                    {
                        ins_run = matrix.GetValue(row - 1, col).Item3;
                        if (matrix.GetValue(row - 1, col).Item1 == 0)
                        {
                            // no penalty to start the alignment
                            ins_val = 0;
                        }
                        else
                        {
                            ins_val = matrix.GetValue(row - 1, col).Item1 + gap_extension_penalty;
                        }

                    }
                    else
                    {
                        ins_val = matrix.GetValue(row - 1, col).Item1 + gap_penalty;
                    }

                    if (matrix.GetValue(row, col - 1).Item2 == 'd')
                    {
                        del_run = matrix.GetValue(row, col - 1).Item3;
                        if (matrix.GetValue(row, col - 1).Item1 == 0)
                        {
                            // no penalty to start the alignment
                            del_val = 0;
                        }
                        else
                        {
                            del_val = matrix.GetValue(row, col - 1).Item1 + gap_extension_penalty;
                        }
                    }
                    else
                    {
                        del_val = matrix.GetValue(row, col - 1).Item1 + gap_penalty;
                    }

                    if (globalalign | full_query)
                    {
                        cell_val = Math.Max(mm_val, Math.Max(del_val, ins_val));
                    }
                    else
                    {
                        cell_val = Math.Max(mm_val, Math.Max(del_val, Math.Max(ins_val, 0)));
                    }

                    if (!prefer_gap_runs)
                    {
                        ins_run = 0;
                        del_run = 0;
                    }

                    if ((del_run != 0) & (cell_val == del_val))
                    {
                        tupValue = new Tuple<double, char, int>(cell_val, 'd', del_run + 1);
                    }
                    else if ((ins_run != 0) & (cell_val == ins_val))
                    {
                        tupValue = new Tuple<double, char, int>(cell_val, 'i', ins_run + 1);
                    }
                    else if (cell_val == mm_val)
                    {
                        tupValue = new Tuple<double, char, int>(cell_val, 'm', 0);
                    }
                    else if (cell_val == del_val)
                    {
                        tupValue = new Tuple<double, char, int>(cell_val, 'd', 1);
                    }
                    else if (cell_val == ins_val)
                    {
                        tupValue = new Tuple<double, char, int>(cell_val, 'i', 1);
                    }
                    else
                    {
                        tupValue = new Tuple<double, char, int>(0, 'x', 0);
                    }

                    if (tupValue.Item1 >= max_val)
                    {
                        max_val = tupValue.Item1;
                        max_row = row;
                        max_col = col;
                    }

                    matrix.SetValue(row, col, tupValue);

                }
            }

            // backtrack
            int back_row, back_col;
            if (globalalign)
            {
                // backtrack from last cell
                back_row = matrix.rows - 1;
                back_col = matrix.columns - 1;
                //back_val = matrix.GetValue(back_row, back_col).Item1;
            }
            else if (full_query)
            {
                // backtrack from max in last row
                back_row = matrix.rows - 1;
                max_val = 0;
                back_col = 0;

                for (int c = 1; c < matrix.columns; c++)
                {
                    if (matrix.GetValue(back_row, c).Item1 > max_val)
                    {
                        back_col = c;
                        max_val = matrix.GetValue(back_row, c).Item1;
                    }
                }

                back_col = matrix.columns - 1;
                //back_val = matrix.GetValue(back_row, back_col).Item1;
            }
            else
            {
                // backtrack from max
                back_row = max_row;
                back_col = max_col;
                //back_val = max_val;
            }

            List<char> aln = new List<char>();
            List<Tuple<int, int>> path = new List<Tuple<int, int>>();
            double back_val;

            while (true)
            {
                Tuple<double, char, int> back_tup = matrix.GetValue(back_row, back_col);
                back_val = back_tup.Item1;
                char op = back_tup.Item2;

                if (globalalign)
                {
                    if ((back_row == 0) & (back_col == 0))
                    {
                        break;
                    }
                }
                else if (full_query)
                {
                    if (back_row == 0)
                    {
                        break;
                    }
                }
                else
                {
                    if (back_val <= 0)
                    {
                        break;
                    }
                }

                path.Add(new Tuple<int, int>(back_row, back_col));
                aln.Add(op);

                if (op == 'm')
                {
                    back_row -= 1;
                    back_col -= 1;
                }
                else if (op == 'i')
                {
                    back_row -= 1;
                }
                else if (op == 'd')
                {
                    back_col -= 1;
                }
                else
                {
                    break;
                }

            }

            aln.Reverse();

            var cigar = ReduceCigar(aln);

            var alignment = new Alignment(origQuery, origRef, back_row, back_col, cigar, max_val, globalalign, wildcard);

            return alignment;

        }

        private static List<Tuple<int, char>> ReduceCigar(List<char> operations)
        {
            int count = 1;
            char? last = null;
            List<Tuple<int, char>> ret = new List<Tuple<int, char>>();

            foreach (char op in operations)
            {
                if ((last != null) & (op == last))
                {
                    count += 1;
                }
                else if (last != null)
                {
                    count = 1;
                }
                last = op;
            }

            if (last != null)
            {
                char nonNullLast = last ?? ' ';
                ret.Add(new Tuple<int, char>(count, Char.ToUpper(nonNullLast)));
            }

            return ret;
        }

        public static bool CheckAlphabet(string inputSeq)
        {
            Regex Validator = new Regex(@"^[ATCGN]+$");

            return Validator.IsMatch(inputSeq);
        }

        public static string ReverseCompliment(string inputSeq)
        {
            string outputSeq = inputSeq;

            outputSeq = outputSeq.Replace('A', 't');
            outputSeq = outputSeq.Replace('T', 'a');
            outputSeq = outputSeq.Replace('G', 'c');
            outputSeq = outputSeq.Replace('C', 'g');

            outputSeq = outputSeq.ToUpper();

            return outputSeq;
        }
    }

    public class Alignment
    {
        string querySeq, refSeq;
        int q_pos, r_pos;
        List<Tuple<int, char>> cigar;
        double score, identity;
        bool globalalign;
        string wildcard;

        int r_offset;
        string q_align_str, m_align_str, r_align_str;
        string orig_query, orig_ref;
        int matches, mismatches;
        int q_end, r_end;

        public Alignment(string querySeq_in,
            string refSeq_in,
            int q_pos_in,
            int r_pos_in,
            List<Tuple<int, char>> cigar_in,
            double score_in,
            bool globalalign_in = false,
            string wildcard_in = "N")
        {
            querySeq = querySeq_in;
            refSeq = refSeq_in;
            q_pos = q_pos_in;
            r_pos = r_pos_in;
            cigar = cigar_in;
            score = score_in;
            globalalign = globalalign_in;
            wildcard = wildcard_in;

            r_offset = 0;

            orig_query = querySeq;
            querySeq = querySeq.ToUpper();

            orig_ref = refSeq;
            refSeq = refSeq.ToUpper();

            int q_len = 0;
            int r_len = 0;

            matches = 0;
            mismatches = 0;

            int i = r_pos;
            int j = q_pos;

            foreach (var cig_tup in cigar)
            {
                int count = cig_tup.Item1;
                char op = cig_tup.Item2;

                if (op == 'M')
                {
                    q_len += count;
                    r_len += count;
                    for (int n = 0; n < count; n++)
                    {
                        if (querySeq[j] == refSeq[i])
                        {
                            matches += 1;
                        }
                        else
                        {
                            mismatches += 1;
                        }
                        i += 1;
                        j += 1;
                    }
                }
                else if (op == 'I')
                {
                    q_len += count;
                    j += count;
                    mismatches += count;
                }
                else if (op == 'D')
                {
                    r_len += count;
                    i += count;
                    mismatches += count;
                }
            }

            q_end = q_pos + q_len;
            r_end = r_pos + r_len;

            if (mismatches + matches > 0)
            {
                identity = (double)matches / ((double)mismatches + (double)matches);
            }
            else
            {
                identity = 0;
            }

            make_alignment_strings();

        }

        void make_alignment_strings()
        {
            int i = r_pos;
            int j = q_pos;

            string q = "";
            string m = "";
            string r = "";
            int qlen = 0;
            int rlen = 0;

            foreach (var cig_tup in cigar)
            {
                int count = cig_tup.Item1;
                char op = cig_tup.Item2;

                if (op == 'M')
                {
                    qlen += count;
                    rlen += count;

                    for (int k = 0; k < count; k++)
                    {
                        q += orig_query[j];
                        r += orig_ref[i];

                        if ((querySeq[j] == refSeq[i]) | ((wildcard != "") & ((wildcard.Contains(querySeq[j])) | (wildcard.Contains(refSeq[i])))))
                        {
                            m += '|';
                        }
                        else
                        {
                            m += '.';
                        }

                        i += 1;
                        j += 1;
                    }
                }
                else if (op == 'D')
                {
                    rlen += count;
                    for (int k = 0; k < count; k++)
                    {
                        q += '-';
                        r += orig_ref[i];
                        m += ' ';
                        i += 1;
                    }
                }
                else if (op == 'I')
                {
                    qlen += count;
                    for (int k = 0; k < count; k++)
                    {
                        q += orig_query[j];
                        r += '-';
                        m += ' ';
                        j += 1;
                    }
                }
                else if (op == 'N')
                {
                    q += "-//-";
                    r += "-//-";
                    m += "    ";
                }
            }

            int rpos = r_pos;
            int qpos = q_pos;

            string q_leader = "";
            string m_leader = "";
            string r_leader = "";

            if (qpos < rpos)
            {
                if (rpos > 0)
                {
                    q_leader += new string(' ', rpos);
                    m_leader += new string(' ', rpos);
                    r_leader += refSeq.Substring(0, rpos);
                }
                if (qpos > 0)
                {
                    q_leader += querySeq.Substring(0, qpos);
                    m_leader += new string(' ', qpos);
                    if (rpos > 0)
                    {
                        r_leader += new string('-', qpos);
                    }
                    else
                    {
                        r_leader += new string(' ', qpos);
                    }
                }
            }
            else
            {
                if (qpos > 0)
                {
                    q_leader += querySeq.Substring(0, qpos);
                    m_leader += new string(' ', qpos);
                    r_leader += new string(' ', qpos);
                }
                if (rpos > 0)
                {
                    r_leader += refSeq.Substring(0, rpos);
                    m_leader += new string(' ', rpos);
                    if (qpos > 0)
                    {
                        q_leader += new string('-', rpos);
                    }
                    else
                    {
                        q_leader += new string(' ', rpos);
                    }
                }


            }

            foreach (char b in q)
            {
                if (b != '-')
                {
                    qpos += 1;
                }
            }

            foreach (char b in r)
            {
                if (b != '-')
                {
                    rpos += 1;
                }
            }

            q_align_str = q_leader + q + querySeq.Substring(qpos);
            r_align_str = r_leader + r + refSeq.Substring(rpos);
            m_align_str = m_leader + m;

            if (q_align_str.Length > r_align_str.Length)
            {
                r_align_str = r_align_str + new string(' ', q_align_str.Length - r_align_str.Length);
                m_align_str = m_align_str + new string(' ', q_align_str.Length - m_align_str.Length);
            }
            else if (r_align_str.Length > q_align_str.Length)
            {
                q_align_str = q_align_str + new string(' ', r_align_str.Length - q_align_str.Length);
                m_align_str = m_align_str + new string(' ', r_align_str.Length - m_align_str.Length);
            }
        }

        public void print_alignment_strings()
        {
            Console.Write(Environment.NewLine);
            Console.WriteLine(q_align_str);
            Console.WriteLine(m_align_str);
            Console.WriteLine(r_align_str);
            Console.WriteLine($"matches: {matches}");
            Console.WriteLine($"identity: {identity:f3}");

            Console.ReadLine();
        }
    }
}

/* Copy of LICENSE from https://github.com/mbreese/swalign:
Copyright (c) 2010-2022 Marcus R. Breese

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

- Redistributions of source code must retain the above copyright
  notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above copyright
  notice, this list of conditions and the following disclaimer listed
  in this license in the documentation and/or other materials
  provided with the distribution.

- Neither the name of the copyright holders nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

The copyright holders provide no reassurances that the source code
provided does not infringe any patent, copyright, or any other
intellectual property rights of third parties.  The copyright holders
disclaim any liability to any recipient for claims brought against
recipient by any third party for infringement of that parties
intellectual property rights.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


 */