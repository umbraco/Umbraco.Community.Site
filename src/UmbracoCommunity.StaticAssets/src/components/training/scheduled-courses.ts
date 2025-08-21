import { LitElement, html } from "lit";
import { customElement, property, queryAssignedElements, state } from "lit/decorators.js";
import { ScheduledCoursesFilter } from "./scheduled-courses.enums";

import "@umbraco-community/css/elements/filtered-table.css";
import { FilterItemGroupElement } from "../filters";

const elementName = "dc-scheduled-courses";

@customElement(elementName)
export class ScheduledCoursesElement extends LitElement {
  @property({ attribute: "user-country" })
  userCountry?: string;

  @state()
  _filters: Array<FilterModel> = [
    {
      alias: ScheduledCoursesFilter.Course,
      label: "Courses",
      defaultValue: "All Courses",
      controlType: "dropdown",
    },
    {
      alias: ScheduledCoursesFilter.Region,
      label: "Regions",
      defaultValue: "All Regions",
      controlType: "dropdown",
    },
    {
      alias: ScheduledCoursesFilter.Query,
      label: "Search",
      tooltip: "Search",
      controlType: "text",
      value: "",
    },
  ];

  render() {
    return html`
      <dc-filters
        .filters=${this._filters}
        .filterType=${ScheduledCoursesFilter}
        .selector=${'[dc-scheduled-course]'}
      >
        <slot></slot>
      </dc-filters>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ScheduledCoursesElement;
  }
}
