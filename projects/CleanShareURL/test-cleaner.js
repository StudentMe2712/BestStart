// Test suite for CleanShare URL cleaning logic
const path = require('path');
const fs = require('fs');

const GLOBAL_TRACKING_PARAMS = new Set([
  'utm_source', 'utm_medium', 'utm_campaign', 'utm_term', 'utm_content', 'utm_id', 'utm_name',
  'utm_reader', 'utm_referrer', 'utm_pubreferrer', 'utm_viz_id', 'utm_source_platform',
  'utm_creative_format', 'utm_marketing_tactic', 'ref', 'source', 'share', 'origin', 'ref_src', 'ref_url',
  'gclid', 'gclsrc', 'dclid', 'gbraid', 'wbraid', 'gad_source', '_ga', '_gl',
  'ga_source', 'ga_medium', 'ga_term', 'ga_content', 'ga_campaign', 'ga_place',
  'fbclid', 'fbadid', 'fb_action_ids', 'fb_action_types', 'fb_source', 'fb_ref',
  'igsh', 'igshid', 'yclid', 'ym_debug', '_openstat', 'from_source', 'from_global',
  'msclkid', 'ms_clkid', 'twclid', 'ttclid', 'li_fat_id', 'trk', 'trkEmail',
  'epik', 'pp', 'mc_cid', 'mc_eid', 'recipient_id', 'campaign_id',
  'ml_subscriber', 'ml_subscriber_hash', 'mkt_tok', '_hsenc', '_hsmi', 'hsCtaTracking',
  'algo_pvid', 'algo_expid', 'btsid', 'ws_ab_test', 'spm', 'scm',
  'aff_platform', 'aff_trace_key', 'aff_short_key', 'click_id', 'clickid', 'tracking_id', 'visitor_id'
]);

const TRACKING_PREFIXES = ['utm_', 'ga_', 'fb_', 'sc_', 'pd_rd_', 'pf_rd_', 'algo_'];

const DOMAIN_RULES = [
  {
    test: /(?:youtube\.com|youtu\.be|music\.youtube\.com)$/i,
    stripParams: [
      'list', 'index', 'ab_channel', 'start_radio', 'rv', 'si', 'feature', 'app', 'pp',
      'embeds_referring_euri', 'embeds_referring_origin', 'source_ve_path', 'theme', 'widget_referrer'
    ]
  },
  {
    test: /spotify\.com$/i,
    stripParams: ['si', 'context']
  },
  {
    test: /(?:twitter\.com|x\.com)$/i,
    stripParams: ['s', 't', 'ref_src', 'ref_url', 'cn']
  },
  {
    test: /(?:reddit\.com|redd\.it)$/i,
    stripParams: ['rdt_cid', 'share_id', 'ref', 'ref_source']
  },
  {
    test: /tiktok\.com$/i,
    stripParams: ['_r', '_t', 'is_from_webapp', 'sender_device', 'share_item_id']
  },
  {
    test: /(?:amazon\.[a-z.]+|amzn\.to)$/i,
    stripParams: ['tag', 'linkcode', 'camp', 'creative', 'creativeasin', 'ref_', '_encoding', 'keywords'],
    cleanPath: (pathname) => pathname.replace(/\/ref=[^/?#]+/i, '')
  },
  {
    test: /(?:aliexpress\.[a-z.]+|aliexpress\.ru)$/i,
    stripParams: ['spm', 'scm', '_t', 'sk', 'aff_trace_key']
  },
  {
    test: /(?:t\.me|telegram\.me)$/i,
    stripParams: ['startattach']
  }
];

const URL_REGEX = /(?:https?:\/\/|www\.)[^\s"'<>()[\]{}]+/gi;

function unwrapRedirect(urlObj) {
  const hostname = urlObj.hostname.toLowerCase();
  if (hostname.includes('google.') && urlObj.pathname === '/url') {
    const target = urlObj.searchParams.get('url') || urlObj.searchParams.get('q');
    if (target && target.startsWith('http')) return target;
  }
  if (hostname.includes('youtube.com') && urlObj.pathname === '/redirect') {
    const target = urlObj.searchParams.get('q');
    if (target && target.startsWith('http')) return target;
  }
  return null;
}

function isTrackingParam(key, hostname) {
  const lowerKey = key.toLowerCase();
  if (GLOBAL_TRACKING_PARAMS.has(lowerKey)) return true;
  for (const prefix of TRACKING_PREFIXES) {
    if (lowerKey.startsWith(prefix)) return true;
  }
  for (const rule of DOMAIN_RULES) {
    if (rule.test.test(hostname)) {
      if (rule.stripParams && rule.stripParams.includes(lowerKey)) return true;
    }
  }
  return false;
}

function cleanUrl(inputUrl) {
  if (!inputUrl || typeof inputUrl !== 'string') {
    return { originalUrl: inputUrl, cleanedUrl: inputUrl, isCleaned: false, removedParams: [], removedCount: 0 };
  }

  let raw = inputUrl.trim();
  const hadNoScheme = !/^https?:\/\//i.test(raw);
  if (hadNoScheme) raw = 'https://' + raw;

  let urlObj;
  try {
    urlObj = new URL(raw);
  } catch (e) {
    return { originalUrl: inputUrl, cleanedUrl: inputUrl, isCleaned: false, removedParams: [], removedCount: 0 };
  }

  const unwrapped = unwrapRedirect(urlObj);
  if (unwrapped) {
    const nestedResult = cleanUrl(unwrapped);
    return {
      originalUrl: inputUrl,
      cleanedUrl: nestedResult.cleanedUrl,
      isCleaned: true,
      removedParams: ['redirect_wrapper', ...nestedResult.removedParams],
      removedCount: 1 + nestedResult.removedCount
    };
  }

  const hostname = urlObj.hostname.toLowerCase();
  const removedParams = [];
  const paramsToKeep = [];

  for (const [key, value] of urlObj.searchParams.entries()) {
    if (isTrackingParam(key, hostname)) {
      removedParams.push(key);
    } else {
      paramsToKeep.push([key, value]);
    }
  }

  urlObj.search = '';
  for (const [key, value] of paramsToKeep) {
    urlObj.searchParams.append(key, value);
  }

  for (const rule of DOMAIN_RULES) {
    if (rule.test.test(hostname) && typeof rule.cleanPath === 'function') {
      const oldPath = urlObj.pathname;
      urlObj.pathname = rule.cleanPath(urlObj.pathname);
      if (oldPath !== urlObj.pathname) removedParams.push('path_ref');
    }
  }

  if (urlObj.hash && urlObj.hash.includes('?')) {
    const [hashPath, hashQuery] = urlObj.hash.split('?');
    const hashParams = new URLSearchParams(hashQuery);
    const hashParamsToKeep = [];
    for (const [key, value] of hashParams.entries()) {
      if (isTrackingParam(key, hostname)) {
        removedParams.push(`hash_${key}`);
      } else {
        hashParamsToKeep.push([key, value]);
      }
    }
    if (hashParamsToKeep.length > 0) {
      urlObj.hash = `${hashPath}?${new URLSearchParams(hashParamsToKeep).toString()}`;
    } else {
      urlObj.hash = hashPath;
    }
  }

  let finalUrl = urlObj.toString().replace(/\?$/, '');
  const isCleaned = removedParams.length > 0 || finalUrl !== inputUrl;

  return {
    originalUrl: inputUrl,
    cleanedUrl: finalUrl,
    isCleaned,
    removedParams,
    removedCount: removedParams.length
  };
}

function cleanTextWithUrls(text) {
  let totalCleanedUrls = 0;
  let totalRemovedParams = 0;
  const cleanedText = text.replace(URL_REGEX, (matchedUrl) => {
    const res = cleanUrl(matchedUrl);
    if (res.isCleaned) {
      totalCleanedUrls++;
      totalRemovedParams += res.removedCount;
      return res.cleanedUrl;
    }
    return matchedUrl;
  });
  return { originalText: text, cleanedText, totalCleanedUrls, totalRemovedParams };
}

// ==========================================
// TEST CASES
// ==========================================
const tests = [
  {
    name: 'YouTube Watch Link with playlist (list=WL) and index (index=12)',
    input: 'https://www.youtube.com/watch?v=QO1uJdA73ts&list=WL&index=12',
    expectedCleaned: 'https://www.youtube.com/watch?v=QO1uJdA73ts',
    expectedTrackers: 2
  },
  {
    name: 'YouTube Watch Link with playlist, index and timestamp (t=45s)',
    input: 'https://www.youtube.com/watch?v=QO1uJdA73ts&list=RDMM&index=3&t=45s',
    expectedCleaned: 'https://www.youtube.com/watch?v=QO1uJdA73ts&t=45s',
    expectedTrackers: 2
  },
  {
    name: 'YouTube Watch Link with si, feature and timestamps',
    input: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ&si=abcdef123456&feature=share&t=42s',
    expectedCleaned: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s',
    expectedTrackers: 2
  },
  {
    name: 'YouTube Short link with si tracker',
    input: 'https://youtu.be/dQw4w9WgXcQ?si=TRACKER_123&t=10',
    expectedCleaned: 'https://youtu.be/dQw4w9WgXcQ?t=10',
    expectedTrackers: 1
  },
  {
    name: 'Spotify Track with si & context',
    input: 'https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=abcde12345&context=spotify%3Aplaylist%3A37i9dQZF1DXcBWIGoYBM5M',
    expectedCleaned: 'https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT',
    expectedTrackers: 2
  },
  {
    name: 'Twitter / X Tweet Link with t, s, ref_src',
    input: 'https://x.com/username/status/1234567890?s=20&t=abcdef123&ref_src=twsrc%5Etfw',
    expectedCleaned: 'https://x.com/username/status/1234567890',
    expectedTrackers: 3
  },
  {
    name: 'Amazon Product URL with /ref= path and query tags',
    input: 'https://www.amazon.com/dp/B08N5WRWNW/ref=cm_sw_r_cp_api_glt_fabc_123?tag=affiliate-20&linkCode=ogi',
    expectedCleaned: 'https://www.amazon.com/dp/B08N5WRWNW',
    expectedTrackers: 3
  },
  {
    name: 'Standard UTM and FBCLID Link preserving search & pagination query params',
    input: 'https://shop.example.com/products?q=shoes&page=2&sort=price_asc&utm_source=facebook&utm_medium=cpc&utm_campaign=summer_sale&fbclid=IwAR123',
    expectedCleaned: 'https://shop.example.com/products?q=shoes&page=2&sort=price_asc',
    expectedTrackers: 4
  },
  {
    name: 'Google Redirect unwrap with UTM stripping',
    input: 'https://www.google.com/url?q=https://myblog.com/post?utm_source=newsletter%26id%3D99&usg=AOvVaw123',
    expectedCleaned: 'https://myblog.com/post?id=99',
    expectedTrackers: 2 // unwrap + utm_source
  },
  {
    name: 'Clean Text with multiple URLs embedded including YouTube playlist link',
    input: 'Watch https://www.youtube.com/watch?v=QO1uJdA73ts&list=WL&index=12 and read https://site.com/art?utm_source=tg&page=1 today!',
    isTextTest: true,
    expectedTotalUrls: 2,
    expectedTotalTrackers: 3
  }
];

console.log('--- RUNNING CLEANSHARE URL VERIFICATION TESTS ---\n');
let passed = 0;
let failed = 0;

for (const t of tests) {
  if (t.isTextTest) {
    const res = cleanTextWithUrls(t.input);
    const pass = res.totalCleanedUrls === t.expectedTotalUrls && res.totalRemovedParams === t.expectedTotalTrackers;
    if (pass) {
      console.log(`[PASS] ${t.name}`);
      passed++;
    } else {
      console.error(`[FAIL] ${t.name}:`, res);
      failed++;
    }
  } else {
    const res = cleanUrl(t.input);
    const pass = res.cleanedUrl === t.expectedCleaned && res.removedCount === t.expectedTrackers;
    if (pass) {
      console.log(`[PASS] ${t.name}`);
      passed++;
    } else {
      console.error(`[FAIL] ${t.name}\n  Expected: ${t.expectedCleaned} (trackers: ${t.expectedTrackers})\n  Got:      ${res.cleanedUrl} (trackers: ${res.removedCount})`);
      failed++;
    }
  }
}

console.log(`\nResults: ${passed} Passed, ${failed} Failed`);
if (failed > 0) process.exit(1);
