import { UMB_AUTH_CONTEXT as ee } from "@umbraco-cms/backoffice/auth";
import { UMB_DOCUMENT_ENTITY_TYPE as U } from "@umbraco-cms/backoffice/document";
import { css as te, state as b, customElement as ae, when as E, html as y } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement as re, UmbModalToken as ie } from "@umbraco-cms/backoffice/modal";
import { tryExecute as z } from "@umbraco-cms/backoffice/resources";
var ne = async (e, t) => {
  let a = typeof t == "function" ? await t(e) : t;
  if (a) return e.scheme === "bearer" ? `Bearer ${a}` : e.scheme === "basic" ? `Basic ${btoa(a)}` : a;
}, se = { bodySerializer: (e) => JSON.stringify(e, (t, a) => typeof a == "bigint" ? a.toString() : a) }, le = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, oe = (e) => {
  switch (e) {
    case "form":
      return ",";
    case "pipeDelimited":
      return "|";
    case "spaceDelimited":
      return "%20";
    default:
      return ",";
  }
}, ue = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, M = ({ allowReserved: e, explode: t, name: a, style: s, value: n }) => {
  if (!t) {
    let i = (e ? n : n.map((o) => encodeURIComponent(o))).join(oe(s));
    switch (s) {
      case "label":
        return `.${i}`;
      case "matrix":
        return `;${a}=${i}`;
      case "simple":
        return i;
      default:
        return `${a}=${i}`;
    }
  }
  let l = le(s), r = n.map((i) => s === "label" || s === "simple" ? e ? i : encodeURIComponent(i) : P({ allowReserved: e, name: a, value: i })).join(l);
  return s === "label" || s === "matrix" ? l + r : r;
}, P = ({ allowReserved: e, name: t, value: a }) => {
  if (a == null) return "";
  if (typeof a == "object") throw new Error("Deeply-nested arrays/objects aren’t supported. Provide your own `querySerializer()` to handle these.");
  return `${t}=${e ? a : encodeURIComponent(a)}`;
}, I = ({ allowReserved: e, explode: t, name: a, style: s, value: n, valueOnly: l }) => {
  if (n instanceof Date) return l ? n.toISOString() : `${a}=${n.toISOString()}`;
  if (s !== "deepObject" && !t) {
    let o = [];
    Object.entries(n).forEach(([f, w]) => {
      o = [...o, f, e ? w : encodeURIComponent(w)];
    });
    let m = o.join(",");
    switch (s) {
      case "form":
        return `${a}=${m}`;
      case "label":
        return `.${m}`;
      case "matrix":
        return `;${a}=${m}`;
      default:
        return m;
    }
  }
  let r = ue(s), i = Object.entries(n).map(([o, m]) => P({ allowReserved: e, name: s === "deepObject" ? `${a}[${o}]` : o, value: m })).join(r);
  return s === "label" || s === "matrix" ? r + i : i;
}, pe = /\{[^{}]+\}/g, ce = ({ path: e, url: t }) => {
  let a = t, s = t.match(pe);
  if (s) for (let n of s) {
    let l = !1, r = n.substring(1, n.length - 1), i = "simple";
    r.endsWith("*") && (l = !0, r = r.substring(0, r.length - 1)), r.startsWith(".") ? (r = r.substring(1), i = "label") : r.startsWith(";") && (r = r.substring(1), i = "matrix");
    let o = e[r];
    if (o == null) continue;
    if (Array.isArray(o)) {
      a = a.replace(n, M({ explode: l, name: r, style: i, value: o }));
      continue;
    }
    if (typeof o == "object") {
      a = a.replace(n, I({ explode: l, name: r, style: i, value: o, valueOnly: !0 }));
      continue;
    }
    if (i === "matrix") {
      a = a.replace(n, `;${P({ name: r, value: o })}`);
      continue;
    }
    let m = encodeURIComponent(i === "label" ? `.${o}` : o);
    a = a.replace(n, m);
  }
  return a;
}, R = ({ allowReserved: e, array: t, object: a } = {}) => (s) => {
  let n = [];
  if (s && typeof s == "object") for (let l in s) {
    let r = s[l];
    if (r != null) if (Array.isArray(r)) {
      let i = M({ allowReserved: e, explode: !0, name: l, style: "form", value: r, ...t });
      i && n.push(i);
    } else if (typeof r == "object") {
      let i = I({ allowReserved: e, explode: !0, name: l, style: "deepObject", value: r, ...a });
      i && n.push(i);
    } else {
      let i = P({ allowReserved: e, name: l, value: r });
      i && n.push(i);
    }
  }
  return n.join("&");
}, de = (e) => {
  if (!e) return "stream";
  let t = e.split(";")[0]?.trim();
  if (t) {
    if (t.startsWith("application/json") || t.endsWith("+json")) return "json";
    if (t === "multipart/form-data") return "formData";
    if (["application/", "audio/", "image/", "video/"].some((a) => t.startsWith(a))) return "blob";
    if (t.startsWith("text/")) return "text";
  }
}, he = async ({ security: e, ...t }) => {
  for (let a of e) {
    let s = await ne(a, t.auth);
    if (!s) continue;
    let n = a.name ?? "Authorization";
    switch (a.in) {
      case "query":
        t.query || (t.query = {}), t.query[n] = s;
        break;
      case "cookie":
        t.headers.append("Cookie", `${n}=${s}`);
        break;
      case "header":
      default:
        t.headers.set(n, s);
        break;
    }
    return;
  }
}, O = (e) => me({ baseUrl: e.baseUrl, path: e.path, query: e.query, querySerializer: typeof e.querySerializer == "function" ? e.querySerializer : R(e.querySerializer), url: e.url }), me = ({ baseUrl: e, path: t, query: a, querySerializer: s, url: n }) => {
  let l = n.startsWith("/") ? n : `/${n}`, r = (e ?? "") + l;
  t && (r = ce({ path: t, url: r }));
  let i = a ? s(a) : "";
  return i.startsWith("?") && (i = i.substring(1)), i && (r += `?${i}`), r;
}, j = (e, t) => {
  let a = { ...e, ...t };
  return a.baseUrl?.endsWith("/") && (a.baseUrl = a.baseUrl.substring(0, a.baseUrl.length - 1)), a.headers = N(e.headers, t.headers), a;
}, N = (...e) => {
  let t = new Headers();
  for (let a of e) {
    if (!a || typeof a != "object") continue;
    let s = a instanceof Headers ? a.entries() : Object.entries(a);
    for (let [n, l] of s) if (l === null) t.delete(n);
    else if (Array.isArray(l)) for (let r of l) t.append(n, r);
    else l !== void 0 && t.set(n, typeof l == "object" ? JSON.stringify(l) : l);
  }
  return t;
}, x = class {
  _fns;
  constructor() {
    this._fns = [];
  }
  clear() {
    this._fns = [];
  }
  getInterceptorIndex(e) {
    return typeof e == "number" ? this._fns[e] ? e : -1 : this._fns.indexOf(e);
  }
  exists(e) {
    let t = this.getInterceptorIndex(e);
    return !!this._fns[t];
  }
  eject(e) {
    let t = this.getInterceptorIndex(e);
    this._fns[t] && (this._fns[t] = null);
  }
  update(e, t) {
    let a = this.getInterceptorIndex(e);
    return this._fns[a] ? (this._fns[a] = t, e) : !1;
  }
  use(e) {
    return this._fns = [...this._fns, e], this._fns.length - 1;
  }
}, ye = () => ({ error: new x(), request: new x(), response: new x() }), fe = R({ allowReserved: !1, array: { explode: !0, style: "form" }, object: { explode: !0, style: "deepObject" } }), _e = { "Content-Type": "application/json" }, k = (e = {}) => ({ ...se, headers: _e, parseAs: "auto", querySerializer: fe, ...e }), be = (e = {}) => {
  let t = j(k(), e), a = () => ({ ...t }), s = (r) => (t = j(t, r), a()), n = ye(), l = async (r) => {
    let i = { ...t, ...r, fetch: r.fetch ?? t.fetch ?? globalThis.fetch, headers: N(t.headers, r.headers) };
    i.security && await he({ ...i, security: i.security }), i.body && i.bodySerializer && (i.body = i.bodySerializer(i.body)), (i.body === void 0 || i.body === "") && i.headers.delete("Content-Type");
    let o = O(i), m = { redirect: "follow", ...i }, f = new Request(o, m);
    for (let c of n.request._fns) c && (f = await c(f, i));
    let w = i.fetch, p = await w(f);
    for (let c of n.response._fns) c && (p = await c(p, f, i));
    let $ = { request: f, response: p };
    if (p.ok) {
      if (p.status === 204 || p.headers.get("Content-Length") === "0") return i.responseStyle === "data" ? {} : { data: {}, ...$ };
      let c = (i.parseAs === "auto" ? de(p.headers.get("Content-Type")) : i.parseAs) ?? "json";
      if (c === "stream") return i.responseStyle === "data" ? p.body : { data: p.body, ...$ };
      let T = await p[c]();
      return c === "json" && (i.responseValidator && await i.responseValidator(T), i.responseTransformer && (T = await i.responseTransformer(T))), i.responseStyle === "data" ? T : { data: T, ...$ };
    }
    let C = await p.text();
    try {
      C = JSON.parse(C);
    } catch {
    }
    let g = C;
    for (let c of n.error._fns) c && (g = await c(C, p, f, i));
    if (g = g || {}, i.throwOnError) throw g;
    return i.responseStyle === "data" ? void 0 : { error: g, ...$ };
  };
  return { buildUrl: O, connect: (r) => l({ ...r, method: "CONNECT" }), delete: (r) => l({ ...r, method: "DELETE" }), get: (r) => l({ ...r, method: "GET" }), getConfig: a, head: (r) => l({ ...r, method: "HEAD" }), interceptors: n, options: (r) => l({ ...r, method: "OPTIONS" }), patch: (r) => l({ ...r, method: "PATCH" }), post: (r) => l({ ...r, method: "POST" }), put: (r) => l({ ...r, method: "PUT" }), request: l, setConfig: s, trace: (r) => l({ ...r, method: "TRACE" }) };
};
const A = be(k({
  baseUrl: "http://localhost:23901",
  throwOnError: !0
}));
class L {
  static postChangeType(t) {
    return (t?.client ?? A).post({
      security: [
        {
          scheme: "bearer",
          type: "http"
        }
      ],
      url: "/umbraco/flip/management/api/v1/change-type",
      ...t,
      headers: {
        "Content-Type": "application/json",
        ...t?.headers
      }
    });
  }
  static getContentModel(t) {
    return (t?.client ?? A).get({
      security: [
        {
          scheme: "bearer",
          type: "http"
        }
      ],
      url: "/umbraco/flip/management/api/v1/content-model",
      ...t
    });
  }
  static getPermitted(t) {
    return (t?.client ?? A).get({
      security: [
        {
          scheme: "bearer",
          type: "http"
        }
      ],
      url: "/umbraco/flip/management/api/v1/permitted",
      ...t
    });
  }
}
const W = "Flip.ChangeDocumentType", ge = [
  {
    type: "entityAction",
    kind: "default",
    name: "Flip Change Document Type Action",
    alias: "Flip.EntityAction.ChangeDocumentType",
    forEntityTypes: [U],
    meta: {
      label: "#flip_changeDocumentType",
      icon: "icon-axis-rotation"
    },
    api: () => import("./change-document-type.action.js"),
    conditions: [
      {
        alias: "Umb.Condition.UserPermission.Document",
        allOf: [W]
      }
    ]
  }
], Te = [
  {
    type: "localization",
    alias: "Flip.Localization.En",
    weight: -100,
    name: "Flip Localization - English",
    meta: {
      culture: "en"
    },
    js: () => import("./en.js")
  }
], F = "Flip.Modal.ChangeDocumentType", ve = [
  {
    type: "modal",
    alias: F,
    name: "Change Document Type Modal",
    js: () => Promise.resolve().then(() => xe)
  }
], we = [
  {
    type: "entityUserPermission",
    alias: "Flip.EntityUserPermission.ChangeDocumentType",
    name: "Flip Change Document Type User Permission",
    forEntityTypes: [U],
    meta: {
      verbs: [W],
      label: "#flip_changeDocumentType",
      description: "#flip_allowChangeDocumentType"
    }
  }
], $e = [
  ...ge,
  ...Te,
  ...ve,
  ...we
];
var Ce = Object.defineProperty, Ee = Object.getOwnPropertyDescriptor, Y = (e) => {
  throw TypeError(e);
}, _ = (e, t, a, s) => {
  for (var n = s > 1 ? void 0 : s ? Ee(t, a) : t, l = e.length - 1, r; l >= 0; l--)
    (r = e[l]) && (n = (s ? r(t, a, n) : r(n)) || n);
  return s && n && Ce(t, a, n), n;
}, D = (e, t, a) => t.has(e) || Y("Cannot " + a), H = (e, t, a) => (D(e, t, "read from private field"), a ? a.call(e) : t.get(e)), q = (e, t, a) => t.has(e) ? Y("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, a), Ae = (e, t, a, s) => (D(e, t, "write to private field"), t.set(e, a), a), d = (e, t, a) => (D(e, t, "access private method"), a), v, u, B, K, G, J, S, V, Q, X, Z;
const Pe = "change-document-type-modal";
let h = class extends re {
  constructor() {
    super(...arguments), q(this, u), this._isLoading = !1, this._permittedTypes = [], this._entity = null, this._newProperties = {}, this._mapType = "DATATYPE", q(this, v);
  }
  async connectedCallback() {
    super.connectedCallback(), this._isLoading = !0, await d(this, u, B).call(this), await d(this, u, K).call(this), this._isLoading = !1;
  }
  render() {
    return y`<umb-body-layout
      .headline=${this.localize.term("flip_changeDocumentType")}
    >
      ${E(
      this._isLoading,
      () => y`<umb-loader></umb-loader>`,
      () => d(this, u, Z).call(this)
    )}
      <div slot="actions">
        <uui-button
          label=${this.localize.term("general_close")}
          @click=${this._rejectModal}
        ></uui-button>
        <uui-button
          color="positive"
          look="primary"
          label=${this.localize.term("general_submit")}
          @click=${d(this, u, Q)}
        ></uui-button>
      </div>
    </umb-body-layout>`;
  }
};
v = /* @__PURE__ */ new WeakMap();
u = /* @__PURE__ */ new WeakSet();
B = async function() {
  const { data: e } = await z(
    this,
    L.getContentModel({
      query: { unique: this.data?.document.unique?.toString() }
    })
  );
  this._entity = e ?? null;
};
K = async function() {
  const { data: e } = await z(
    this,
    L.getPermitted({
      query: { unique: this.data?.document.unique?.toString() }
    })
  );
  this._permittedTypes = e ?? [], this._isLoading = !1, this._permittedTypes.length === 1 && (this._targetType = this._permittedTypes[0], d(this, u, S).call(this, !0));
};
G = function(e) {
  this._targetType = this._permittedTypes.find(
    (t) => t.unique === e.target.value
  ), d(this, u, S).call(this, !0);
};
J = function(e) {
  this._mapType = e.target.value, this._targetType && d(this, u, S).call(this);
};
S = function(e = !1) {
  this._entity && (this._newProperties = {}, e && Ae(this, v, this._targetType?.defaultTemplateId), this._entity.properties?.forEach((t) => {
    t.newAlias = "";
  }), this._targetType?.propertyTypes.forEach((t) => {
    const a = this._mapType === "DATATYPE" ? t.dataTypeKey : t.propertyEditorAlias;
    if (!a) return;
    this._newProperties[a] || (this._newProperties[a] = [
      { dataTypeKey: "", editor: "", alias: "", label: "" }
    ]), this._newProperties[a]?.push({
      dataTypeKey: t.dataTypeKey,
      editor: t.propertyEditorAlias,
      alias: t.alias,
      label: t.name
    });
    const s = (l) => this._mapType === "DATATYPE" ? l.dataTypeKey === t.dataTypeKey : l.editor === t.propertyEditorAlias, n = this._entity.properties?.find(
      (l) => l.alias === t.alias && s(l)
    ) ?? this._entity.properties?.find((l) => s(l));
    n && (n.newAlias = t.alias);
  }));
};
V = function(e) {
  return this._newProperties[this._mapType === "DATATYPE" ? e.dataTypeKey : e.editor] ?? [];
};
Q = function() {
  this.updateValue({
    contentTypeUnique: this._targetType.unique,
    templateId: H(this, v),
    properties: this._entity?.properties ?? []
  }), this._submitModal();
};
X = function() {
  return y`<umb-table
      .config=${{ allowSelection: !1, hideIcon: !0 }}
      .columns=${[
    {
      alias: "current",
      name: this.localize.term("flip_currentProperty")
    },
    {
      alias: "new",
      name: this.localize.term("flip_newProperty")
    }
  ]}
      .items=${this._entity?.properties?.map((e) => {
    const t = d(this, u, V).call(this, e);
    return {
      id: e.alias,
      data: [
        { columnAlias: "current", value: e.label },
        {
          columnAlias: "new",
          value: y`<uui-select
                .options=${t.map((a) => ({
            name: a.label,
            value: a.alias,
            selected: a.alias === e.newAlias
          })) ?? []}
                @change=${(a) => {
            e.newAlias = a.target.value;
          }}
                ?disabled=${!t.length}
              ></uui-select>`
        }
      ]
    };
  }) ?? []}
    >
    </umb-table>`;
};
Z = function() {
  return y` ${E(
    !this._permittedTypes.length,
    () => y` <uui-box
          >${this.localize.term("flip_noPermittedTypes")}</uui-box
        >`,
    () => y`<uui-box .headline=${this.localize.term("general_settings")}
          ><umb-property-layout .label=${this.localize.term("flip_newType")}>
            <uui-select
              slot="editor"
              ?disabled=${this._permittedTypes.length === 1}
              .options=${this._permittedTypes.map((e) => ({
      name: e.name,
      value: e.unique,
      selected: e.unique === this._targetType?.unique
    }))}
              @change=${d(this, u, G)}
            ></uui-select>
          </umb-property-layout>
          ${E(
      this._targetType,
      (e) => y` <umb-property-layout
                .label=${this.localize.term("flip_newTemplate")}
              >
                <uui-select
                  slot="editor"
                  ?disabled=${e.allowedTemplates.length === 1}
                  .options=${e.allowedTemplates.map((t) => ({
        name: t.name,
        value: t.id.toString(),
        selected: t.id === H(this, v)
      }))}
                ></uui-select>
              </umb-property-layout>
              <umb-property-layout .label=${this.localize.term("flip_mapType")}>
                <div slot="editor">
                  <uui-radio-group
                    @change=${d(this, u, J)}
                    .value=${this._mapType}
                  >
                    <uui-radio
                      label="Data Type"
                      value=${"DATATYPE"}
                    ></uui-radio>
                    <uui-radio
                      label="Property Editor"
                      value=${"EDITOR"}
                    ></uui-radio>
                  </uui-radio-group>
                </div>
              </umb-property-layout>
              <small>${this.localize.term("flip_mapTypeInstruction")}</small>`
    )}
        </uui-box>

        ${E(
      this._targetType,
      () => y`<uui-box
            .headline=${this.localize.term("flip_mapProperties")}
          >
            <small
              >${this.localize.term("flip_mapPropertiesInstruction")}</small
            >
            ${d(this, u, X).call(this)}</uui-box
          >`
    )} `
  )}`;
};
h.styles = te`
    uui-box + uui-box {
      margin-top: var(--uui-size-5);
    }

    umb-table {
      display: block;
      margin-top: var(--uui-size-5);
    }
  `;
_([
  b()
], h.prototype, "_isLoading", 2);
_([
  b()
], h.prototype, "_permittedTypes", 2);
_([
  b()
], h.prototype, "_targetType", 2);
_([
  b()
], h.prototype, "_entity", 2);
_([
  b()
], h.prototype, "_newProperties", 2);
_([
  b()
], h.prototype, "_mapType", 2);
h = _([
  ae(Pe)
], h);
const Se = h, xe = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  get ChangeDocumentTypeModalElement() {
    return h;
  },
  default: Se
}, Symbol.toStringTag, { value: "Module" })), ze = new ie(F, {
  modal: {
    type: "sidebar",
    size: "small"
  }
}), Me = (e, t) => {
  t.registerMany($e), e.consumeContext(ee, async (a) => {
    if (!a) return;
    const s = a?.getOpenApiConfiguration();
    A.setConfig({
      baseUrl: s?.base ?? "",
      auth: s?.token ?? void 0,
      credentials: s?.credentials ?? "same-origin"
    });
  });
};
export {
  F as C,
  L as F,
  h as a,
  ze as b,
  Me as o
};
//# sourceMappingURL=index.js.map
