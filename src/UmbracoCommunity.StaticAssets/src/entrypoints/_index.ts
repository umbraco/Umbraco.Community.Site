/**
 * This is the base JS entry point for site-wide features.
 * It is loaded in the base layout and will be available on all pages
 */

import "../css/styles.css";
import '../components/index.js';
import "../integrations/index.js";
import { FAQsAccordion } from "../components/faqs-accordion.element";
import { polyfillCountryFlagEmojis } from "country-flag-emoji-polyfill";
import { listenForMemberAvatarChanges } from "../util/member-avatar-sync";

polyfillCountryFlagEmojis("Twemoji Country Flags", "/fonts/TwemojiCountryFlags.woff2");

import {
  setUtmCookies,
  utmTransfer,
  LocaleResolver,
} from "@umbraco-community/util";

import '@umbraco-ui/uui-loader';
import '@ungap/custom-elements';

// must be available globally for dc-currency elements to reference
window.localeResolver = new LocaleResolver();

// Update the header avatar straight away when the member uploads or removes their avatar.
listenForMemberAvatarChanges();

function getClickedWebComponentElement(e: Event): HTMLElement | undefined {
  const path = e.composedPath();
  const idx = path.findIndex(
    (x) => (x as HTMLElement).nodeName === "#document-fragment"
  );

  if (idx === -1) return;

  const element = path.find((x) => (x as HTMLElement).nodeName === "A");
  return element ? (element as HTMLElement) : undefined;
}

function createObserver(element: HTMLElement, observer: IntersectionObserver | undefined, callback, options?: IntersectionObserverInit) {
  if (!element) return;
  if (observer) observer.disconnect();

  options = options || {
    threshold: Array.from({length: 1000}, (_, i) => i / 1000),
  };

  observer = new IntersectionObserver(
    (entries) => entries.forEach(callback),
    options
  );

  observer.observe(element);
}

function initializeNavScrollingBehavior() {
  const scrollClassName = 'scroll';
  const body = document.body;
  let scrollDownObserver: IntersectionObserver;

  createObserver(document.querySelector(".nav-header__pointer")!, scrollDownObserver!, (entry) => {
    if (entry.isIntersecting && body.classList.contains(scrollClassName)) {
      toggleWithGlide(() => body.classList.remove(scrollClassName));
    } else if (!entry.isIntersecting && !body.classList.contains(scrollClassName)) {
      toggleWithGlide(() => body.classList.add(scrollClassName));
    }
  });
}

function toggleWithGlide(toggle: () => void) {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    toggle();
  } else if (document.body.classList.contains("mobile")) {
    toggleWithLogoScale(toggle);
  } else {
    toggleWithLinkSlide(toggle);
  }
}

// The mobile logo's size comes from its viewBox, which CSS can't transition, so scale it from its old size.
function toggleWithLogoScale(toggle: () => void) {
  const logo = document.querySelector<SVGSVGElement>(".nav-header .nav-mobile .logo-link svg");
  if (!logo) {
    toggle();
    return;
  }

  const viewBox = logo.viewBox.baseVal;
  const drawnScale = (r: DOMRect) => Math.min(r.width / viewBox.width, r.height / viewBox.height);

  const before = logo.getBoundingClientRect();
  toggle();
  const after = logo.getBoundingClientRect();
  if (!viewBox?.width || !drawnScale(after)) return;

  const scale = drawnScale(before) / drawnScale(after);
  const dx = before.left + before.width / 2 - (after.left + after.width / 2);
  const dy = before.top + before.height / 2 - (after.top + after.height / 2);

  logo.animate(
    [{ transform: `translate(${dx}px, ${dy}px) scale(${scale})` }, { transform: "none" }],
    { duration: 300, easing: "ease" }
  );
}

// The nav links switch between right-aligned and centred, which CSS can't transition, so slide them across.
function toggleWithLinkSlide(toggle: () => void) {
  const list = document.querySelector<HTMLElement>(".nav-header .nav-list__list");
  const first = list?.firstElementChild;
  if (!list || !first) {
    toggle();
    return;
  }

  const offset = () => first.getBoundingClientRect().left - list.getBoundingClientRect().left;
  const before = offset();
  toggle();
  const dx = before - offset();

  if (Math.abs(dx) > 1) {
    list.animate(
      [{ transform: `translateX(${dx}px)` }, { transform: "translateX(0)" }],
      { duration: 300, easing: "ease" }
    );
  }
}

/**
 * Keeps --live-nav-height in sync with the header's actual rendered height
 * (which varies by breakpoint and scrolled/unscrolled state), so in-page
 * anchor jumps and scrollIntoView() calls can offset for the sticky nav via
 * `scroll-padding-top: var(--live-nav-height)` on <html> instead of relying
 * on a single guessed pixel value.
 */
function initializeLiveNavHeightTracking() {
  const header = document.querySelector(".nav-header .header");
  if (!header) return;

  const root = document.documentElement;
  const resizeObserver = new ResizeObserver(([entry]) => {
    root.style.setProperty("--live-nav-height", `${Math.ceil(entry.target.getBoundingClientRect().height)}px`);
  });
  resizeObserver.observe(header);
}

function initatilzePlanComparisionTable() {
  const activeClassName = 'active';
  const expandedClassName = 'expanded';
  const components = document.querySelectorAll(".plan-comparison__table");
  if (components.length === 0) return;

  components.forEach((component) => {
    const planComparisionContainer = component.parentElement as HTMLElement;
    const expanderLinks = planComparisionContainer.querySelectorAll(".plan-comparison__expander span");
    const mobileNavItems = planComparisionContainer.querySelectorAll('.plan-comparison__table-mobile-heading li');
    const dataColumns = component.querySelectorAll('tbody td[data-plan-id]');

    const clearActiveClass = (elements: NodeListOf<Element>) =>
      elements.forEach((el) =>
        el.classList.remove(activeClassName));

    const setActiveClassByPlanId = (planId: string, elements: NodeListOf<Element>) => {
      [...elements]
        .filter(n => n.getAttribute("data-plan-id") === planId)
        .forEach((el) => el.classList.add(activeClassName));
    };

    expanderLinks.forEach((expanderLink) =>
      expanderLink.addEventListener("click", (e) => {
          if (planComparisionContainer.classList.contains(expandedClassName)) {
            planComparisionContainer.classList.remove(expandedClassName);
            const timeout = setTimeout(() => {
              planComparisionContainer.scrollIntoView({behavior: 'smooth'});
              clearTimeout(timeout);
            }, 500);
          } else {
            planComparisionContainer.classList.add(expandedClassName);
          }
        }
      )
    );

    mobileNavItems.forEach((mobileNavItem) =>
      mobileNavItem.addEventListener("click", () => {
        const planId = mobileNavItem.getAttribute("data-plan-id");
        if (!planId) return;
        clearActiveClass(mobileNavItems);
        clearActiveClass(dataColumns);
        setActiveClassByPlanId(planId, mobileNavItems);
        setActiveClassByPlanId(planId, dataColumns);
      })
    );
  });
}

function initializeBase() {
  document.addEventListener("click", (e: Event) => {
    const element = getClickedWebComponentElement(e);
    if (!element) return;

    const data = {
      "gtm.element": element,
      "gtm.elementUrl": element.getAttribute("href"),
      "gtm.elementText": element.innerText,
      "gtm.elementClasses": element.className,
      "gtm.elementId": element.id,
      "gtm.willOpenInNewWindow": element.getAttribute("_target") === "blank",
      event: "gtm.linkClick",
    };

    window.dataLayer = window.dataLayer || [];
    window.dataLayer.push(data);
  });

  initializeNavScrollingBehavior();
  initializeLiveNavHeightTracking();
  setUtmCookies();
}

function initializePostponed() {
  utmTransfer();
  initatilzePlanComparisionTable();
  
  // Initialize FAQ accordions (image variant - single open)
  const imageAccordions = document.querySelectorAll('.dc-faqs-and-image-block');
  imageAccordions.forEach(container => {
    new FAQsAccordion(container as HTMLElement, { singleOpenOnly: true });
  });

  // Initialize FAQ accordions (standalone variant - multi open)
  const standaloneAccordions = document.querySelectorAll('.dc-faqs:not(.dc-faqs-and-image-block .dc-faqs)');
  standaloneAccordions.forEach(container => {
    new FAQsAccordion(container as HTMLElement, { singleOpenOnly: false });
  });
}

document.addEventListener("DOMContentLoaded", () => {
  initializeBase();
  setTimeout(initializePostponed, 1000);
});

declare global {
  interface Window {
    dataLayer: any[];
    localeResolver: LocaleResolver;
  }
}
