import CookieHelper from './cookie';
import { querySelectorAllDeep } from 'query-selector-shadow-dom';

const UTM_PARAMS = {
    UTM_SOURCE: 'utm_source',
    UTM_MEDIUM: 'utm_medium',
    UTM_CAMPAIGN: 'utm_campaign'
};

const DEFAULT_UTM_VALUES = {
    DEFAULT_SOURCE: '(direct)',
    DEFAULT_CAMPAIGN: '(not set)',
    DEFAULT_MEDIUM: '(none)'
};

const UTM_MEDIUM_FALLBACK_VALUES = {
    REFERRAL: 'referral',
    ORGANIC: 'organic',
    EMAIL: 'email'
};

const CPC_CAMPAIGN = {
    GOOGLE_CLICK_ID: 'gclid',
    BING_CLICK_ID: 'msclkid',
    PAY_PER_CLICK: 'cpc',
    GOOGLE: 'google',
    BING: 'bing'
};

/**
 * Checks whether the source contains any of the strings provided in a list.
 * @param {array} source string containing the source.
 * @param {array} list list of strings.
 * @returns {boolean} whether the list contains the source.
 */
const doesListContainSource = (source, list) => {
    const sourceId = source
        .split(' ')
        .join('')
        .toLowerCase();

    return list.includes(sourceId);
};

/**
 * Checks whether the url contains a parameter or not.
 * @param {boolean} param the parameter to check
 * @returns {boolean} whether the url contains the param or not
 */
const hasUrlParam = param => {
    const querystring = window.location.search.replace('?', '');
    let hasParam = false;

    if (querystring.length > 0) {
        hasParam = querystring.split('&').some(pair => pair.startsWith(param));
    }

    return hasParam;
};

export default class UtmParams {
    /**
     * Generates object of utm_parameters in the url.
     * @returns {object} inlcuding utm params
     * @example {utm_source: newsletter, utm_medium: email, utm_campaign: summer_sale }
     */
    static getParams = () => {
        const querystring = window.location.search.replace('?', '');
        const utmParams = {};

        if (querystring.length < 1) return utmParams;

        querystring
            .split('&')
            .filter(pair => pair.startsWith('utm_'))
            .forEach(pair => {
                var keyval = pair.split('=');
                utmParams[keyval[0]] = keyval[1];
            });

        return utmParams;
    };

    /**
     * Checks whether the source is a search engine or not.
     * @param {string} source string containing the source.
     * @returns {boolean} whether the source is a search engine or not.
     */
    static isSearchEngine = source => {
        const searchEngines = ['google', 'duckduckgo', 'baidu', 'yahoo', 'yandex', 'bing'];

        return doesListContainSource(source, searchEngines);
    };

    /**
     * Checks whether the source is a email campaign or not.
     * @param {string} source string containing the source.
     * @returns {boolean} whether the source is a email campaign or not.
     */
    static isEmailCampaign = source => {
        const campaignTypes = ['activecampaign', 'campaignmonitor'];

        return doesListContainSource(source, campaignTypes);
    };

    /**
     * Checks whether the source is a social media or not.
     * @param {string} source string containing the source.
     * @returns {boolean} whether the source is a social media or not.
     */
    static isSocialMedia = source => {
        const socialMediaList = ['linkedin', 'facebook', 'twitter', 'instagram'];

        return doesListContainSource(source, socialMediaList);
    };

    /**
     * Checks whether the source is a backoffice dashboard
     * @param {string} source string containing the source.
     * @returns {boolean} whether the source is a backoffice dashboard or not.
     */
    static isBackofficeDashboard = source => {
        const backofficeDashboards = ['core', 'cloud', 'uno', 'heartcore'];

        return doesListContainSource(source, backofficeDashboards);
    };
}

const { UTM_SOURCE, UTM_MEDIUM, UTM_CAMPAIGN } = UTM_PARAMS;
const { GOOGLE_CLICK_ID, BING_CLICK_ID } = CPC_CAMPAIGN;
const { getCookie, setCookie } = CookieHelper;
const utmParams = UtmParams.getParams();
const daysActive = 30;
const isGoogleAd = hasUrlParam(GOOGLE_CLICK_ID);
const isBingAd = hasUrlParam(BING_CLICK_ID);
const { hostname } = window.location;
const { hostname: referrerHostname } = document.referrer ? new URL(document.referrer) : new URL('https://umbraco.com');
const isSelfReferral = hostname === referrerHostname;
const sourceCookie = getCookie(UTM_SOURCE);
const mediumCookie = getCookie(UTM_MEDIUM);
const campaignCookie = getCookie(UTM_CAMPAIGN);

export const getUtmSourceValue = () => sourceCookie;
export const getUtmMediumValue = () => mediumCookie;
export const getUtmCampaignValue = () => campaignCookie;

/**
 * Stores utm values in cookies.
 * One cookie for each value (utm_source, utm_medium, utm_campaign)
 */
export const setUtmCookies = () => {
    setSourceCookie();
    setMediumCookie();
    setCampaignCookie();
};

/**
 * Sets the source cookie based on where the user is comming from.
 * Current cookie should be overwritten, unless the user is comming directly.
 */
const setSourceCookie = () => {
    const { DEFAULT_SOURCE } = DEFAULT_UTM_VALUES;
    const { GOOGLE, BING } = CPC_CAMPAIGN;
    const utmSourceFromParam = utmParams[UTM_SOURCE];

    if (utmSourceFromParam) {
        setCookie(UTM_SOURCE, utmSourceFromParam, daysActive);
    } else if (isGoogleAd) {
        setCookie(UTM_SOURCE, GOOGLE, daysActive);
    } else if (isBingAd) {
        setCookie(UTM_SOURCE, BING, daysActive);
    } else if (referrerHostname && !isSelfReferral) {
        setCookie(UTM_SOURCE, referrerHostname, daysActive);
        // We don't want to overwrite the value to (direct) if a cookie from another source is set
    } else if (!sourceCookie) {
        setCookie(UTM_SOURCE, DEFAULT_SOURCE, daysActive);
    }
};

/**
 * Sets the madium cookie based on where the user is comming from.
 * Current cookie should be overwritten, unless the user is comming directly.
 */
const setMediumCookie = () => {
    const { DEFAULT_MEDIUM } = DEFAULT_UTM_VALUES;
    const { REFERRAL, EMAIL, ORGANIC } = UTM_MEDIUM_FALLBACK_VALUES;
    const { PAY_PER_CLICK } = CPC_CAMPAIGN;
    const utmMediumFromParam = utmParams[UTM_MEDIUM];
    const { isSearchEngine, isSocialMedia, isEmailCampaign } = UtmParams;

    if (utmMediumFromParam) {
        setCookie(UTM_MEDIUM, utmMediumFromParam, daysActive);
    } else if (isGoogleAd || isBingAd) {
        setCookie(UTM_MEDIUM, PAY_PER_CLICK, daysActive);
    } else if (referrerHostname && !isSelfReferral) {
        setCookie(UTM_MEDIUM, REFERRAL, daysActive);
    } else if (isSearchEngine(sourceCookie) || isSocialMedia(sourceCookie)) {
        setCookie(UTM_MEDIUM, ORGANIC, daysActive);
    } else if (isEmailCampaign(sourceCookie)) {
        setCookie(UTM_MEDIUM, EMAIL, daysActive);
        // We don't want to overwrite the value to (not set) if a cookie from another source is set
    } else if (!mediumCookie) {
        setCookie(UTM_MEDIUM, DEFAULT_MEDIUM, daysActive);
    }
};

/**
 * Sets the campaign cookie based on where the user is comming from.
 * Current cookie should be overwritten, unless the user is comming directly.
 */
const setCampaignCookie = () => {
    const { DEFAULT_CAMPAIGN } = DEFAULT_UTM_VALUES;
    const utmCampaignFromParam = utmParams[UTM_CAMPAIGN];
    const { pathname } = window?.location;

    if (utmCampaignFromParam) {
        setCookie(UTM_CAMPAIGN, utmCampaignFromParam, daysActive);
    } else if (isGoogleAd || isBingAd) {
        setCookie(UTM_CAMPAIGN, pathname, daysActive);
        // We don't want to overwrite the value to (none) if a cookie from another source is set
    } else if (!campaignCookie) {
        setCookie(UTM_CAMPAIGN, DEFAULT_CAMPAIGN, daysActive);
    }
};

export const utmTransfer = () => {
    var utmInheritingDomains = [
        'try.umbraco.com', // Try Umbraco
        'calendly.com' // Calendly
    ],
        utmRegExp = /(\&|\?)utm_[A-Za-z]+=[A-Za-z0-9]+/gi,
        links = querySelectorAllDeep('a'),
        utms = [
            `utm_source=${sourceCookie}`,
            `utm_medium=${mediumCookie}`,
            `utm_campaign=${campaignCookie}`
        ];

    for (var index = 0; index < links.length; index += 1) {
        let tempLink = links[index].href;

        // if the current link is not in the utm domains array, go next
        if (!utmInheritingDomains.some(d => tempLink.includes(d))) {
            continue;
        }

        // The script is looking for all links with the utmInheritingDomain
        tempLink = tempLink.replace(utmRegExp, '');
        const tempParts = tempLink.split('#');

        // Append UTM parameters to existing query, or start fresh
        tempParts[0] += (tempParts[0].startsWith('?') ? '&' : '?') + utms.join('&');

        links[index].href = decodeURI(tempParts.join('#'));
    }
};

export const getTransferedUtmParams = (url: String) => {
    var utmInheritingDomains = [
        'try.umbraco.com', // Try Umbraco
        'calendly.com' // Calendly
    ],
        utmRegExp = /(\&|\?)utm_[A-Za-z]+=[A-Za-z0-9]+/gi,
        utms = [
            `utm_source=${sourceCookie}`,
            `utm_medium=${mediumCookie}`,
            `utm_campaign=${campaignCookie}`
        ];

    if (!utmInheritingDomains.some(d => url.includes(d))) {
        return url;
    }

    url = url.replace(utmRegExp, '');

    const tempParts = url.split('#');

    tempParts[0] += (tempParts[0].startsWith('?') ? '&' : '?') + utms.join('&');

    return decodeURI(tempParts.join('#'));
};