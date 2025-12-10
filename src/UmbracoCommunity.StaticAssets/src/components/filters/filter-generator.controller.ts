type FilterType = Record<string, string>;
type FilterValues<T = string | Array<string>> = Record<string, T>;

export class FilterGeneratorController {
  readonly #optionTypes = ["select", "checkbox", "checkboxlist", "dropdown"];
  readonly #arrayTypes = ["checkboxlist", "dropdown"];

  #filterType: FilterType;

  constructor(filterType: FilterType) {
    this.#filterType = filterType;
  }

  generate(items: Array<HTMLElement>, config: Array<FilterModel>) {
    config.forEach((c) => {
      if (!this.#optionTypes.includes(c.controlType)) return;

      c.options = this.#generateFilterOptions(items, c.alias, c.defaultValue, c.sortOrder);
      const value = c.options?.filter((o) => o.selected)?.map((o) => o.value);
      c.value = this.#arrayTypes.includes(c.controlType) ? value : value?.at(0);
    });

    return config;
  }

  isArrayValueType(filter: FilterModel) {
    return filter.value
      ? Array.isArray(filter.value)
      : this.#arrayTypes.includes(filter.controlType);
  }

  setQueryString(filters: Array<FilterModel>) {
    const qs = filters
      .reduce((acc: Array<string>, { alias, value }) => {
        if (value?.length) {
          // coerces array to comma-delimited string
          // only encode non-empty values
          const v = `${value}`;
          acc.push(
            `${alias}=${
              v.length
                ? FilterGeneratorController.getEncodedUrlParamValue(v, alias)
                : v
            }`
          );
        }
        return acc;
      }, [])
      .join("&");

    window.history.pushState({}, "", `?${qs}`);
  }

  valueMatch<T>(filterValue: FilterValues<T>, element: HTMLElement) {
    const visible = Object.values(this.#filterType).map((v) => {
      const matchValue = filterValue[v];
      
      let elementValue = element.getAttribute(v) ?? "";
      elementValue = elementValue.startsWith('[') ? JSON.parse(elementValue) : elementValue;

      // when querying, the match is limited to the properties
      // provided in the query prop on the element.
      if (v === "q" && element.getAttribute("query")) {
        const queryableProps = element.getAttribute("query")?.split(",") ?? [];
        const query = new RegExp(matchValue as string, "i");

        return queryableProps.some((p) =>
          query.test((element.getAttribute(p) ?? "").toLocaleLowerCase())
        );
      }

      // either or both can be arrays, so we need multiple match clauses
      // to ensure the provided values are both arrays
      return Array.isArray(matchValue) && Array.isArray(elementValue)
        ? this.arrayValueMatch(matchValue, element[v])
        : Array.isArray(matchValue)
        ? this.arrayValueMatch(matchValue, [elementValue])
        : Array.isArray(elementValue) && typeof matchValue === "string"
        ? this.arrayValueMatch([matchValue], elementValue)
        : matchValue === elementValue;
    });

    return visible.every((x) => x);
  }

  arrayValueMatch(match: Array<string>, value?: Array<string>) {
    if (!match?.length) return false;
    if (match.length === 1 && match.at(0) === "") return true;

    return (
      value?.some((x) => {
        const v = FilterGeneratorController.getEncodedUrlParamValue(x)?.toLowerCase();
        if (!v) return false;
        return match.some(m => m.toLowerCase() === v);
      }) ?? false
    );
  }

  #generateFilterOptions(
    slotItems: Array<HTMLElement>,
    propName: string,
    defaultValue?: string | null,
    sortOrder?: Array<string>
  ): Array<Option> {
    const result: Array<Option> = defaultValue
      ? [
          {
            name: defaultValue,
            value: "",
            selected: true,
          },
        ]
      : [];

    const values = [
      ...new Set(
        slotItems
          .map((n) => {
            const p = n.getAttribute(propName);
            return p?.startsWith("[") ? JSON.parse(p) : p;
          })
          .flat()
      ),
    ];

    const options: Array<Option> = this.#sortValues(values, sortOrder)
      .filter((name) => name && name !== "null" && name !== "")
      .map((name) => ({
        name: name!,
        value: FilterGeneratorController.getEncodedUrlParamValue(name!) as any,
        selected: !defaultValue,
      }));

    return [...result, ...options];
  }

  #sortValues(values: Array<string | undefined>, sortOrder?: Array<string>) {
    if (!sortOrder?.length) {
      return [...values].sort();
    }

    const order = sortOrder.map((v) => v.toLowerCase());

    return [...values].sort((a, b) => {
      const aStr = a ?? "";
      const bStr = b ?? "";

      const aIndex = order.indexOf(aStr.toLowerCase());
      const bIndex = order.indexOf(bStr.toLowerCase());

      const aInOrder = aIndex !== -1;
      const bInOrder = bIndex !== -1;

      if (aInOrder && bInOrder) return aIndex - bIndex;
      if (aInOrder) return -1;
      if (bInOrder) return 1;

      return aStr.localeCompare(bStr);
    });
  }

  /* when encoding a query, do not replace any chars as this prevents decoding as
  the decoded string will differ, which then prevents deep-linking to a result sets */
  static getEncodedUrlParamValue(value: string, alias?: string) {
    if (!value) return;

    return encodeURIComponent(
      alias === "q"
        ? value.toLocaleLowerCase()
        : value.toLocaleLowerCase().replaceAll(" ", "-")
    );
  }

  static isVisible(x: Element) {
    return x.getAttribute("filter-out") === null;
  }

  static set(x: Element, visible: boolean) {
    visible
      ? x.removeAttribute("filter-out")
      : x.setAttribute("filter-out", "true");
  }
}
