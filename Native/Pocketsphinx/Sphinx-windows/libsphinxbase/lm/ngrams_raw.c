/* -*- c-basic-offset: 4; indent-tabs-mode: nil -*- */
/* ====================================================================
 * Copyright (c) 2015 Carnegie Mellon University.  All rights
 * reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer. 
 *
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in
 *    the documentation and/or other materials provided with the
 *    distribution.
 *
 * This work was supported in part by funding from the Defense Advanced 
 * Research Projects Agency and the National Science Foundation of the 
 * United States of America, and the CMU Sphinx Speech Consortium.
 *
 * THIS SOFTWARE IS PROVIDED BY CARNEGIE MELLON UNIVERSITY ``AS IS'' AND 
 * ANY EXPRESSED OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
 * THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 * PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL CARNEGIE MELLON UNIVERSITY
 * NOR ITS EMPLOYEES BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY 
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * ====================================================================
 *
 */

#include <string.h>
#include <assert.h>

#include <err.h>
#include <pio.h>
#include <strfuncs.h>
#include <ckd_alloc.h>
#include <priority_queue.h>
#include <byteorder.h>

#include "ngram_model_internal.h"
#include "ngrams_raw.h"

int
ngram_comparator(const void *first_void, const void *second_void)
{
    static int order = -1;
    uint32 *first, *second, *end;

    if (first_void == NULL) {
        //technical usage, setuping order
        order = *(int *) second_void;
        return 0;
    }
    if (order < 2) {
        E_ERROR("Order for ngram comprator was not set\n");
        return 0;
    }
    first = ((ngram_raw_t *) first_void)->words;
    second = ((ngram_raw_t *) second_void)->words;
    end = first + order;
    for (; first != end; ++first, ++second) {
        if (*first < *second)
            return -1;
        if (*first > *second)
            return 1;
    }
    return 0;
}

int
ngram_ord_comparator(void *a_raw, void *b_raw)
{
    ngram_raw_ord_t *a = (ngram_raw_ord_t *) a_raw;
    ngram_raw_ord_t *b = (ngram_raw_ord_t *) b_raw;
    int a_w_ptr = 0;
    int b_w_ptr = 0;
    while (a_w_ptr < a->order && b_w_ptr < b->order) {
        if (a->instance.words[a_w_ptr] == b->instance.words[b_w_ptr]) {
            a_w_ptr++;
            b_w_ptr++;
            continue;
        }
        if (a->instance.words[a_w_ptr] < b->instance.words[b_w_ptr])
            return 1;
        else
            return -1;
    }
    return b->order - a->order;
}

static void
read_ngram_instance(lineiter_t ** li, hash_table_t * wid,
                    logmath_t * lmath, int order, int order_max,
                    ngram_raw_t * raw_ngram)
{
    int n;
    int words_expected;
    int i;
    char *wptr[NGRAM_MAX_ORDER + 1];
    uint32 *word_out;

    *li = lineiter_next(*li);
    if (*li == NULL) {
        E_ERROR("Unexpected end of ARPA file. Failed to read %d-gram\n",
                order);
        return;
    }
    string_trim((*li)->buf, STRING_BOTH);
    words_expected = order + 1;

    if ((n =
         str2words((*li)->buf, wptr,
                   NGRAM_MAX_ORDER + 1)) < words_expected) {
        if ((*li)->buf[0] != '\0') {
            E_WARN("Format error; %d-gram ignored: %s\n", order,
                   (*li)->buf);
        }
    }
    else {
        if (order == order_max) {
            raw_ngram->weights =
                (float *) ckd_calloc(1, sizeof(*raw_ngram->weights));
            raw_ngram->weights[0] = atof_c(wptr[0]);
            if (raw_ngram->weights[0] > 0) {
                E_WARN("%d-gram [%s] has positive probability. Zeroize\n",
                       order, wptr[1]);
                raw_ngram->weights[0] = 0.0f;
            }
            raw_ngram->weights[0] =
                logmath_log10_to_log_float(lmath, raw_ngram->weights[0]);
        }
        else {
            float weight, backoff;
            raw_ngram->weights =
                (float *) ckd_calloc(2, sizeof(*raw_ngram->weights));

            weight = atof_c(wptr[0]);
            if (weight > 0) {
                E_WARN("%d-gram [%s] has positive probability. Zeroize\n",
                       order, wptr[1]);
                raw_ngram->weights[0] = 0.0f;
            }
            else {
                raw_ngram->weights[0] =
                    logmath_log10_to_log_float(lmath, weight);
            }

            if (n == order + 1) {
                raw_ngram->weights[1] = 0.0f;
            }
            else {
                backoff = atof_c(wptr[order + 1]);
                raw_ngram->weights[1] =
                    logmath_log10_to_log_float(lmath, backoff);
            }
        }
        raw_ngram->words =
            (uint32 *) ckd_calloc(order, sizeof(*raw_ngram->words));
        for (word_out = raw_ngram->words + order - 1, i = 1;
             word_out >= raw_ngram->words; --word_out, i++) {
            hash_table_lookup_int32(wid, wptr[i], (int32 *) word_out);
        }
    }
}

static void
ngrams_raw_read_order(ngram_raw_t ** raw_ngrams, lineiter_t ** li,
                      hash_table_t * wid, logmath_t * lmath, uint32 count,
                      int order, int order_max)
{
    char expected_header[20];
    uint32 i;

    sprintf(expected_header, "\\%d-grams:", order);
    while ((*li = lineiter_next(*li))) {
        string_trim((*li)->buf, STRING_BOTH);
        if (strcmp((*li)->buf, expected_header) == 0)
            break;
    }
    *raw_ngrams = (ngram_raw_t *) ckd_calloc(count, sizeof(ngram_raw_t));
    for (i = 0; i < count; i++) {
        read_ngram_instance(li, wid, lmath, order, order_max,
                            &((*raw_ngrams)[i]));
    }

    //sort raw ngrams that was read
    ngram_comparator(NULL, &order);     //setting up order in comparator
    qsort(*raw_ngrams, count, sizeof(ngram_raw_t), &ngram_comparator);
}

ngram_raw_t **
ngrams_raw_read_arpa(lineiter_t ** li, logmath_t * lmath, uint32 * counts,
                     int order, hash_table_t * wid)
{
    ngram_raw_t **raw_ngrams;
    int order_it;

    raw_ngrams =
        (ngram_raw_t **) ckd_calloc(order - 1, sizeof(*raw_ngrams));
    for (order_it = 2; order_it <= order; order_it++) {
        ngrams_raw_read_order(&raw_ngrams[order_it - 2], li, wid, lmath,
                              counts[order_it - 1], order_it, order);
    }
    //check for end-mark in arpa file
    *li = lineiter_next(*li);
    string_trim((*li)->buf, STRING_BOTH);
    //skip empty lines if any
    while (*li && strlen((*li)->buf) == 0) {
        *li = lineiter_next(*li);
        string_trim((*li)->buf, STRING_BOTH);
    }
    //check if we finished reading
    if (*li == NULL)
        E_ERROR("ARPA file ends without end-mark\n");
    //check if we found ARPA end-mark
    if (strcmp((*li)->buf, "\\end\\") != 0)
        E_ERROR
            ("Finished reading ARPA file. Expecting end mark but found [%s]\n",
             (*li)->buf);

    return raw_ngrams;
}

static void
read_dmp_weight_array(FILE * fp, logmath_t * lmath, uint8 do_swap,
                      int32 counts, ngram_raw_t * raw_ngrams,
                      int weight_idx)
{
    int32 i, k;
    dmp_weight_t *tmp_weight_arr;

    fread(&k, sizeof(k), 1, fp);
    if (do_swap)
        SWAP_INT32(&k);
    tmp_weight_arr =
        (dmp_weight_t *) ckd_calloc(k, sizeof(*tmp_weight_arr));
    fread(tmp_weight_arr, sizeof(*tmp_weight_arr), k, fp);
    for (i = 0; i < k; i++) {
        if (do_swap)
            SWAP_INT32(&tmp_weight_arr[i].l);
        /* Convert values to log. */
        tmp_weight_arr[i].f =
            logmath_log10_to_log_float(lmath, tmp_weight_arr[i].f);
    }
    //replace indexes with real probs in raw bigrams
    for (i = 0; i < counts; i++) {
        raw_ngrams[i].weights[weight_idx] =
            tmp_weight_arr[(int) raw_ngrams[i].weights[weight_idx]].f;
    }
    ckd_free(tmp_weight_arr);
}

#define BIGRAM_SEGMENT_SIZE 9

ngram_raw_t **
ngrams_raw_read_dmp(FILE * fp, logmath_t * lmath, uint32 * counts,
                    int order, uint32 * unigram_next, uint8 do_swap)
{
    int i;
    uint32 j, ngram_idx;
    uint16 *bigrams_next;
    ngram_raw_t **raw_ngrams =
        (ngram_raw_t **) ckd_calloc(order - 1, sizeof(*raw_ngrams));

    //read bigrams
    raw_ngrams[0] =
        (ngram_raw_t *) ckd_calloc((size_t) (counts[1] + 1),
                                   sizeof(*raw_ngrams[0]));
    bigrams_next =
        (uint16 *) ckd_calloc((size_t) (counts[1] + 1),
                              sizeof(*bigrams_next));
    ngram_idx = 1;
    for (j = 0; j <= (int32) counts[1]; j++) {
        uint16 wid, prob_idx, bo_idx;
        ngram_raw_t *raw_ngram = &raw_ngrams[0][j];

        fread(&wid, sizeof(wid), 1, fp);
        if (do_swap)
            SWAP_INT16(&wid);
        raw_ngram->words =
            (uint32 *) ckd_calloc(2, sizeof(*raw_ngram->words));
        raw_ngram->words[0] = (uint32) wid;
        while (ngram_idx < counts[0] && j == unigram_next[ngram_idx]) {
            ngram_idx++;
        }
        raw_ngram->words[1] = (uint32) ngram_idx - 1;
        raw_ngram->weights =
            (float *) ckd_calloc(2, sizeof(*raw_ngram->weights));
        fread(&prob_idx, sizeof(prob_idx), 1, fp);
        if (do_swap)
            SWAP_INT16(&prob_idx);
        raw_ngram->weights[0] = prob_idx + 0.5f;        //keep index in float. ugly but avoiding using extra memory
        fread(&bo_idx, sizeof(bo_idx), 1, fp);
        if (do_swap)
            SWAP_INT16(&bo_idx);
        raw_ngram->weights[1] = bo_idx + 0.5f;  //keep index in float. ugly but avoiding using extra memory
        fread(&bigrams_next[j], sizeof(bigrams_next[j]), 1, fp);
        if (do_swap)
            SWAP_INT16(&bigrams_next[j]);
    }
    assert(ngram_idx == counts[0]);

    //read trigrams
    if (order > 2) {
        raw_ngrams[1] =
            (ngram_raw_t *) ckd_calloc((size_t) counts[2],
                                       sizeof(*raw_ngrams[1]));
        for (j = 0; j < (int32) counts[2]; j++) {
            uint16 wid, prob_idx;
            ngram_raw_t *raw_ngram = &raw_ngrams[1][j];

            fread(&wid, sizeof(wid), 1, fp);
            if (do_swap)
                SWAP_INT16(&wid);
            raw_ngram->words =
                (uint32 *) ckd_calloc(3, sizeof(*raw_ngram->words));
            raw_ngram->words[0] = (uint32) wid;
            raw_ngram->weights =
                (float *) ckd_calloc(1, sizeof(*raw_ngram->weights));
            fread(&prob_idx, sizeof(prob_idx), 1, fp);
            if (do_swap)
                SWAP_INT16(&prob_idx);
            raw_ngram->weights[0] = prob_idx + 0.5f;    //keep index in float. ugly but avoiding using extra memory
        }
    }

    //read prob2
    read_dmp_weight_array(fp, lmath, do_swap, (int32) counts[1],
                          raw_ngrams[0], 0);
    //read bo2
    if (order > 2) {
        int32 k;
        int32 *tseg_base;
        read_dmp_weight_array(fp, lmath, do_swap, (int32) counts[1],
                              raw_ngrams[0], 1);
        //read prob3
        read_dmp_weight_array(fp, lmath, do_swap, (int32) counts[2],
                              raw_ngrams[1], 0);
        /* Read tseg_base size and tseg_base to fill trigram's first words */
        fread(&k, sizeof(k), 1, fp);
        if (do_swap)
            SWAP_INT32(&k);
        tseg_base = (int32 *) ckd_calloc(k, sizeof(int32));
        fread(tseg_base, sizeof(int32), k, fp);
        if (do_swap) {
            for (j = 0; j < (uint32) k; j++) {
                SWAP_INT32(&tseg_base[j]);
            }
        }
        ngram_idx = 0;
        for (j = 1; j <= counts[1]; j++) {
            uint32 next_ngram_idx =
                (uint32) (tseg_base[j >> BIGRAM_SEGMENT_SIZE] +
                          bigrams_next[j]);
            while (ngram_idx < next_ngram_idx) {
                raw_ngrams[1][ngram_idx].words[1] =
                    raw_ngrams[0][j - 1].words[0];
                raw_ngrams[1][ngram_idx].words[2] =
                    raw_ngrams[0][j - 1].words[1];
                ngram_idx++;
            }
        }
        ckd_free(tseg_base);
        assert(ngram_idx == counts[2]);
    }
    ckd_free(bigrams_next);

    //sort raw ngrams for reverse trie
    i = 2;                      //set order
    ngram_comparator(NULL, &i);
    qsort(raw_ngrams[0], (size_t) counts[1], sizeof(*raw_ngrams[0]),
          &ngram_comparator);
    if (order > 2) {
        i = 3;                  //set order
        ngram_comparator(NULL, &i);
        qsort(raw_ngrams[1], (size_t) counts[2], sizeof(*raw_ngrams[1]),
              &ngram_comparator);
    }
    return raw_ngrams;
}

void
ngrams_raw_fix_counts(ngram_raw_t ** raw_ngrams, uint32 * counts,
                      uint32 * fixed_counts, int order)
{
    priority_queue_t *ngrams =
        priority_queue_create(order - 1, &ngram_ord_comparator);
    uint32 raw_ngram_ptrs[NGRAM_MAX_ORDER - 1];
    uint32 words[NGRAM_MAX_ORDER];
    int i;

    memset(words, -1, sizeof(words));   //since we have unsigned word idx that will give us unreachable maximum word index
    memcpy(fixed_counts, counts, order * sizeof(*fixed_counts));
    for (i = 2; i <= order; i++) {
        ngram_raw_ord_t *tmp_ngram;
        
        if (counts[i - 1] <= 0)
            continue;
        tmp_ngram =
            (ngram_raw_ord_t *) ckd_calloc(1, sizeof(*tmp_ngram));
        tmp_ngram->order = i;
        raw_ngram_ptrs[i - 2] = 0;
        tmp_ngram->instance = raw_ngrams[i - 2][0];
        priority_queue_add(ngrams, tmp_ngram);
    }

    for (;;) {
        int32 to_increment = TRUE;
        ngram_raw_ord_t *top;
        if (priority_queue_size(ngrams) == 0) {
            break;
        }
        top = (ngram_raw_ord_t *) priority_queue_poll(ngrams);
        if (top->order == 2) {
            memcpy(words, top->instance.words, 2 * sizeof(*words));
        }
        else {
            for (i = 0; i < top->order - 1; i++) {
                if (words[i] != top->instance.words[i]) {
                    int num;
                    num = (i == 0) ? 1 : i;
                    memcpy(words, top->instance.words,
                           (num + 1) * sizeof(*words));
                    fixed_counts[num]++;
                    to_increment = FALSE;
                    break;
                }
            }
            words[top->order - 1] = top->instance.words[top->order - 1];
        }
        if (to_increment) {
            raw_ngram_ptrs[top->order - 2]++;
        }
        if (raw_ngram_ptrs[top->order - 2] < counts[top->order - 1]) {
            top->instance =
                raw_ngrams[top->order - 2][raw_ngram_ptrs[top->order - 2]];
            priority_queue_add(ngrams, top);
        }
        else {
            ckd_free(top);
        }
    }

    assert(priority_queue_size(ngrams) == 0);
    priority_queue_free(ngrams, NULL);
}

void
ngrams_raw_free(ngram_raw_t ** raw_ngrams, uint32 * counts, int order)
{
    uint32 num;
    int order_it;

    for (order_it = 0; order_it < order - 1; order_it++) {
        for (num = 0; num < counts[order_it + 1]; num++) {
            ckd_free(raw_ngrams[order_it][num].weights);
            ckd_free(raw_ngrams[order_it][num].words);
        }
        ckd_free(raw_ngrams[order_it]);
    }
    ckd_free(raw_ngrams);
}
