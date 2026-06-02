/**
 * This is the base JS entry point for site-wide features.
 * It is loaded in the base layout and will be available on all pages
 */

import "../css/styles.css";
import '../components/index.js';
import "../integrations/index.js";
import { FAQsAccordion } from "../components/faqs-accordion.element";
import { polyfillCountryFlagEmojis } from "country-flag-emoji-polyfill";

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
      body.classList.remove(scrollClassName);
    } else if (!entry.isIntersecting && !body.classList.contains(scrollClassName)) {
      body.classList.add(scrollClassName);
    }
  });
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
