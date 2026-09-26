#!/usr/bin/env bash
# Captures the Open Library cassette corpus for Phase 3 mapper / schema-drift
# tests. Run from repo root:
#
#   bash scripts/capture-ol-cassettes.sh
#
# Writes JSON files to src/NzbDrone.Core.Test/Files/OpenLibrary/. Re-runs
# skip files that already exist (idempotent). Failures (404, network errors)
# are logged to stderr but do not abort the run; check the final summary.
#
# Rate: 0.5s sleep between calls. ~110 files total → ~60-70s wall clock.
#
# Per Phase 3 exit criterion in MASTER-PLAN.md: corpus must cover fiction
# (canonical), non-fiction, audiobook editions, foreign-language works,
# pseudonymous authors, prolific authors, and search/isbn shape coverage.

set -u

OUT_DIR="src/NzbDrone.Core.Test/Files/OpenLibrary"
BASE="https://openlibrary.org"
SLEEP=0.5

mkdir -p "$OUT_DIR"

total=0
captured=0
skipped=0
failed=0

# Capture one URL → file. $1 = endpoint path (relative to BASE), $2 = filename.
capture() {
    local path="$1"
    local file="$2"
    local out="$OUT_DIR/$file"
    total=$((total + 1))

    if [[ -f "$out" ]]; then
        skipped=$((skipped + 1))
        return 0
    fi

    local code
    code=$(curl -sL --max-time 15 \
                -H "User-Agent: Librarr-cassette-capture/1.0 (+https://github.com/)" \
                -o "$out" \
                -w "%{http_code}" \
                "$BASE/$path") || {
        echo >&2 "  [ERR] curl failed for $path"
        rm -f "$out"
        failed=$((failed + 1))
        return 1
    }

    if [[ "$code" != "200" ]]; then
        echo >&2 "  [$code] $path"
        rm -f "$out"
        failed=$((failed + 1))
        return 1
    fi

    # OL sometimes returns a non-JSON HTML error page with a 200 status.
    # Guard against it.
    if ! python3 -c "import json,sys; json.load(open(sys.argv[1]))" "$out" 2>/dev/null; then
        echo >&2 "  [BAD JSON] $path"
        rm -f "$out"
        failed=$((failed + 1))
        return 1
    fi

    captured=$((captured + 1))
    sleep "$SLEEP"
}

echo "== Works (fiction canonical) =="
# (W key, label) pairs — labels match probed search.json results
capture "works/OL46125W.json"      "work_foundation.json"
capture "works/OL46224W.json"      "work_foundation_and_empire.json"
capture "works/OL46309W.json"      "work_second_foundation.json"
capture "works/OL893415W.json"     "work_dune.json"
capture "works/OL1168083W.json"    "work_1984.json"
capture "works/OL103123W.json"     "work_fahrenheit_451.json"
capture "works/OL15358691W.json"   "work_way_of_kings.json"
capture "works/OL16813053W.json"   "work_words_of_radiance.json"
capture "works/OL17834026W.json"   "work_oathbringer.json"
capture "works/OL20842226W.json"   "work_rhythm_of_war.json"
capture "works/OL27482W.json"      "work_hobbit.json"
capture "works/OL27513W.json"      "work_fellowship_of_the_ring.json"
capture "works/OL27479W.json"      "work_two_towers.json"
capture "works/OL27455W.json"      "work_return_of_the_king.json"
capture "works/OL257943W.json"     "work_game_of_thrones.json"
capture "works/OL1963268W.json"    "work_hyperion.json"
capture "works/OL453658W.json"     "work_mort.json"
capture "works/OL453735W.json"     "work_guards_guards.json"
capture "works/OL453662W.json"     "work_hogfather.json"
capture "works/OL49488W.json"      "work_enders_game.json"
capture "works/OL2163649W.json"    "work_hitchhikers_guide.json"
capture "works/OL17914663W.json"   "work_all_systems_red.json"
capture "works/OL17267881W.json"   "work_three_body_problem.json"
capture "works/OL5734647W.json"    "work_old_mans_war.json"
capture "works/OL66554W.json"      "work_pride_and_prejudice.json"
capture "works/OL450063W.json"     "work_frankenstein.json"
capture "works/OL64365W.json"      "work_brave_new_world.json"
capture "works/OL15936512W.json"   "work_ready_player_one.json"
capture "works/OL8479867W.json"    "work_name_of_the_wind.json"
capture "works/OL5738148W.json"    "work_final_empire.json"
capture "works/OL679333W.json"     "work_neverwhere.json"
capture "works/OL679360W.json"     "work_american_gods.json"
capture "works/OL453936W.json"     "work_good_omens.json"
capture "works/OL38501W.json"      "work_snow_crash.json"
capture "works/OL2918756W.json"    "work_shogun.json"
capture "works/OL5781992W.json"    "work_kite_runner.json"
capture "works/OL40873W.json"      "work_the_road.json"
capture "works/OL40879W.json"      "work_blood_meridian.json"
capture "works/OL2943602W.json"    "work_infinite_jest.json"
capture "works/OL81618W.json"      "work_the_stand.json"
capture "works/OL81613W.json"      "work_it.json"
capture "works/OL16002468W.json"   "work_11_22_63.json"
capture "works/OL81628W.json"      "work_gunslinger.json"
capture "works/OL81626W.json"      "work_carrie.json"
capture "works/OL81633W.json"      "work_shining.json"

echo "== Works (non-fiction) =="
capture "works/OL17075811W.json"   "work_sapiens.json"
capture "works/OL1892617W.json"    "work_brief_history_of_time.json"
capture "works/OL1966488W.json"    "work_selfish_gene.json"
capture "works/OL15829966W.json"   "work_cosmos.json"
capture "works/OL15992072W.json"   "work_thinking_fast_and_slow.json"
capture "works/OL20168133W.json"   "work_why_we_sleep.json"
capture "works/OL5749847W.json"    "work_outliers.json"
capture "works/OL17892614W.json"   "work_bad_blood.json"
capture "works/OL18139176W.json"   "work_educated.json"
capture "works/OL29922995W.json"   "work_becoming.json"
capture "works/OL17824318W.json"   "work_born_a_crime.json"
capture "works/OL81601W.json"      "work_on_writing.json"
capture "works/OL716850W.json"     "work_godel_escher_bach.json"
capture "works/OL17184556W.json"   "work_elon_musk.json"
capture "works/OL7920347W.json"    "work_elegant_universe.json"

echo "== Works (foreign-language) =="
capture "works/OL503666W.json"     "work_don_quixote_spanish.json"
capture "works/OL1230613W.json"    "work_letranger_french.json"
capture "works/OL10263W.json"      "work_petit_prince_french.json"
capture "works/OL498463W.json"     "work_der_prozess_german.json"
capture "works/OL267096W.json"     "work_anna_karenina_russian.json"
capture "works/OL2625457W.json"    "work_norwegian_wood_japanese.json"

echo "== Works (pseudonymous) =="
capture "works/OL149210W.json"     "work_thinner_bachman.json"      # King as Bachman
capture "works/OL16806416W.json"   "work_cuckoos_calling_galbraith.json"  # Rowling as Galbraith
capture "works/OL472688W.json"     "work_absent_spring_westmacott.json"   # Christie as Westmacott

echo "== Editions (per work) =="
# Subset of high-value works — exercises edition selection heuristic.
capture "works/OL46125W/editions.json?limit=20"     "editions_foundation.json"
capture "works/OL893415W/editions.json?limit=20"    "editions_dune.json"
capture "works/OL1168083W/editions.json?limit=20"   "editions_1984.json"
capture "works/OL27482W/editions.json?limit=20"     "editions_hobbit.json"
capture "works/OL27513W/editions.json?limit=20"     "editions_fellowship.json"
capture "works/OL257943W/editions.json?limit=20"    "editions_game_of_thrones.json"
capture "works/OL15358691W/editions.json?limit=20"  "editions_way_of_kings.json"
capture "works/OL66554W/editions.json?limit=20"     "editions_pride_and_prejudice.json"
capture "works/OL450063W/editions.json?limit=20"    "editions_frankenstein.json"
capture "works/OL2163649W/editions.json?limit=20"   "editions_hitchhikers_guide.json"
capture "works/OL17075811W/editions.json?limit=20"  "editions_sapiens.json"
capture "works/OL103123W/editions.json?limit=20"    "editions_fahrenheit_451.json"
capture "works/OL10263W/editions.json?limit=20"     "editions_petit_prince_french.json"
capture "works/OL49488W/editions.json?limit=20"     "editions_enders_game.json"
capture "works/OL81618W/editions.json?limit=20"     "editions_the_stand.json"

echo "== Authors =="
# Prolific authors — exercises author + works pagination
capture "authors/OL34221A.json"                     "author_asimov.json"
capture "authors/OL34221A/works.json?limit=50"      "author_asimov_works.json"
capture "authors/OL19981A.json"                     "author_stephen_king.json"
capture "authors/OL19981A/works.json?limit=50"      "author_stephen_king_works.json"
capture "authors/OL1394865A.json"                   "author_sanderson.json"
capture "authors/OL1394865A/works.json?limit=50"    "author_sanderson_works.json"
capture "authors/OL53305A.json"                     "author_gaiman.json"
capture "authors/OL53305A/works.json?limit=50"      "author_gaiman_works.json"
capture "authors/OL26320A.json"                     "author_tolkien.json"
capture "authors/OL26320A/works.json?limit=50"      "author_tolkien_works.json"
capture "authors/OL25712A.json"                     "author_pratchett.json"
capture "authors/OL25712A/works.json?limit=50"      "author_pratchett_works.json"
capture "authors/OL79034A.json"                     "author_frank_herbert.json"

echo "== Search responses (book) =="
capture "search.json?q=foundation+asimov&limit=5&fields=key,title,author_name,author_key,first_publish_year,isbn,cover_i,edition_count"  "search_foundation_asimov.json"
capture "search.json?q=dune+herbert&limit=5&fields=key,title,author_name,author_key,first_publish_year,isbn,cover_i,edition_count"        "search_dune_herbert.json"
capture "search.json?q=1984+orwell&limit=5&fields=key,title,author_name,author_key,first_publish_year,isbn,cover_i,edition_count"         "search_1984_orwell.json"
capture "search.json?q=sapiens+harari&limit=5&fields=key,title,author_name,author_key,first_publish_year,isbn,cover_i,edition_count"      "search_sapiens.json"
capture "search.json?q=norwegian+wood+murakami&limit=5&fields=key,title,author_name,author_key,first_publish_year,isbn,cover_i,edition_count" "search_norwegian_wood.json"

echo "== Search responses (author) =="
capture "search/authors.json?q=isaac+asimov&limit=5"     "search_author_asimov.json"
capture "search/authors.json?q=stephen+king&limit=5"     "search_author_king.json"
capture "search/authors.json?q=brandon+sanderson&limit=5" "search_author_sanderson.json"
capture "search/authors.json?q=neil+gaiman&limit=5"      "search_author_gaiman.json"

echo "== ISBN lookups =="
capture "isbn/9780553293357.json"   "isbn_foundation_9780553293357.json"
capture "isbn/9780441172719.json"   "isbn_dune_9780441172719.json"
capture "isbn/9780451524935.json"   "isbn_1984_9780451524935.json"
capture "isbn/9780261103573.json"   "isbn_hobbit_9780261103573.json"
capture "isbn/9780062316097.json"   "isbn_sapiens_9780062316097.json"

echo "== ASIN search responses =="
capture "search.json?q=identifier%3AB07XKQTFTL&limit=5" "search_asin_audiobook_1.json"
capture "search.json?q=identifier%3AB000FC1MCS&limit=5" "search_asin_audiobook_2.json"

echo "== Edge cases =="
# Known redirect record (probed earlier — OL45883W → OL45804W)
capture "works/OL45883W.json"       "work_redirect_record.json"
# Subject browse (used by trending / list features)
capture "subjects/science_fiction.json?limit=10"  "subject_science_fiction.json"
capture "subjects/fantasy.json?limit=10"          "subject_fantasy.json"
# Trending list (Phase 6 import lists)
capture "trending/now.json?limit=10"              "trending_now.json"

echo
echo "============================================"
echo "Capture summary"
echo "  total attempted: $total"
echo "  captured (new): $captured"
echo "  skipped (exists): $skipped"
echo "  failed:         $failed"
echo "============================================"

# Exit non-zero only if everything failed (network outage) so we don't
# block development for individual 404s.
if [[ "$captured" == "0" && "$skipped" == "0" ]]; then
    exit 1
fi
exit 0
