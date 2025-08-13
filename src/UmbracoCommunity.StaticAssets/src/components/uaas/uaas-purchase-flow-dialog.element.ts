import { css, html } from "lit";
import { customElement, state } from "lit/decorators.js";
import { DcDialogBaseElement } from "../dialog/dialog-base.element";
import {
  fetch,
  ProjectAvailabilityService,
  ProjectNameService,
  ProjectService,
  UserService,
} from "@umbraco-community/services";
import { when } from "lit/directives/when.js";
import {
  PurchaseFlowArgs,
  PurchaseFlowForm,
  PurchaseFlowLog,
} from "./entities";

import "@umbraco-ui/uui-form";
import "@umbraco-ui/uui-label";
import "@umbraco-ui/uui-form-layout-item";
import "@umbraco-ui/uui-progress-bar";

const elementName = "dc-purchase-flow-dialog";

@customElement(elementName)
export class DcPurchaseFlowDialogElement extends DcDialogBaseElement {
  @state()
  private _args?: PurchaseFlowArgs;

  @state()
  private _progress = {
    step: 0,
    percentage: 0,
    message: "",
  };

  @state()
  private _form: PurchaseFlowForm = {
    name: null,
    email: null,
    password: null,
    consent: false,
  };

  @state()
  private _log: PurchaseFlowLog = {};

  constructor(args: PurchaseFlowArgs) {
    super();
    this._args = args;
    this.header = this._args.planTitle ?? this._args.plan ?? "";
  }

  renderBody() {
    switch (this._progress.step) {
      case 0:
        return this.#renderStep0();
      case 1:
        return this.#renderStep1();
      case 99:
        return this.#renderStep99();
    }
  }

  #setProgress(message: string, percentage: number) {
    this._progress = { ...this._progress, ...{ message, percentage } };
  }

  #submitForm(e) {
    e.preventDefault();
    this._progress.step += 1;
    this.#checkForProjects();
  }

  async #checkForProjects() {
    this.#setProgress("Making sure we have a project for you", 10);

    const { error } = await fetch(
      ProjectAvailabilityService.checkAvailability(
        this._args?.sku,
        this._args?.plan
      )
    );

    if (error) {
      this.#setError({ reason: "Unable to check for projects" });
      return;
    }

    this.#validateEmail();
  }

  #reset() {
    this.header = this._args?.planTitle ?? this._args?.plan ?? "";
    this._progress = { step: 0, percentage: 0, message: "" };
    this._log = {};
    this._form = { name: null, email: null, password: null, consent: false };
  }

  #validateEmail() {
    this.#setProgress("Validating user information", 15);

    const errorHandler = () =>
      this.#setError({ reason: "Unable to verify email" });

    UserService.canUseEmail(this._form.email, this._args?.sku)
      .then((response) => {
        if (response.status == 200 || response.status == 201) {
          this.#createUser();
        } else if (response.status == 302) {
          this.#authorizeUser();
        } else {
          errorHandler();
        }
      })
      .catch(errorHandler);
  }

  async #authorizeUser() {
    this.#setProgress(
      "Looks like you have an account. Trying to authenticate",
      20
    );

    const { error } = await fetch(
      UserService.authorizeUser(this._form.email, this._form.password)
    );

    if (error) {
      this.#setError({
        reason: "Unable to auth user",
        title: "An account with this email already exists",
        description:
          "The email you have entered is already associated with an Umbraco Account. Please use the password you use for that account to continue, or use a different email address.",
      });
      return;
    }

    this._progress = {
      ...this._progress,
      ...{ message: "Authenticated successfully..." },
    };

    this.#generateProjectName();
  }

  async #createUser() {
    this.#setProgress("Creating your account", 20);

    const { error } = await fetch(
      UserService.createUser(
        this._form.name,
        this._form.email,
        this._form.password
      )
    );

    if (error) {
      this.#setError({ reason: "Unable to create user" });
      return;
    }

    this.#generateProjectName();
  }

  async #generateProjectName() {
    this._progress = {
      ...this._progress,
      ...{ message: "Generating a project name" },
    };

    const errorLogger = (e?: any) => {
      console.log("Unable to generate project name", e);
    };

    const { data, error } = await fetch(
      ProjectNameService.getProjectName(this._form.name!)
    );

    if (error) {
      errorLogger(error);
      return;
    }

    ProjectNameService.isProjectNameAvailable(
      encodeURIComponent(data.projectName!),
      encodeURIComponent(this._form.email!)
    )
      .then(
        () => this.#createProject(data.projectName),
        () => this.#generateProjectName()
      )
      .catch(errorLogger);
  }

  async #createProject(projectName?: string) {
    this.#setProgress(`Creating your ${this._args?.planTitle ?? this._args?.plan} project`, 64);

    const { data, error } = await fetch(
      ProjectService.create(
        projectName!,
        this._args?.sku!,
        this._args?.plan!,
        this._form.email!
      )
    );

    if (error) {
      this.#setError({ reason: "Unable to create project" });
      return;
    }

    this.#checkProjectStatus(data.projectId);
  }

  async #checkProjectStatus(projectId: string) {
    this.#setProgress("Checking if your project is ready", 81);

    const { data, error } = await fetch(
      ProjectService.checkProjectReady(projectId)
    );

    if (error) {
      this.#setError({ reason: "Unable to check project status" });
      return;
    }

    if (data.projectIsReady) {
      this.#setProgress("Redirecting to the shop", 100);
      window.location.href = data.paymentLink;
    } else {
      setTimeout(() => this.#checkProjectStatus(projectId), 5000);
    }
  }

  #setError(args: { reason?: string; title?: string; description?: string }) {
    this.header = args.title ?? "Sorry, we could not create a project for you";
    this._progress = { ...this._progress, ...{ step: 99 } };
    this._log = { ...args };
  }

  #disableFormSubmit() {
    const form = this.shadowRoot?.querySelector("form");
    return form ? form?.checkValidity() === false : true;
  }

  #setFormValue(e: Event) {
    const { value, id } = e.target as any;
    this._form = { ...this._form, ...{ [id]: value } };
  }

  #renderInput(name: string) {
    const lowerName = name.toLowerCase();
    const type = name === "Name" ? "text" : lowerName;

    return html`<uui-form-layout-item>
      <uui-label for=${lowerName} slot="label">${name}</uui-label>
      <uui-input
        .type=${type as any}
        id=${lowerName}
        placeholder=${name}
        @change=${this.#setFormValue}
        .value=${this._form[lowerName]}
        required
      ></uui-input>
    </uui-form-layout-item>`;
  }

  #renderStep0() {
    return html`<p class="lead">
        You're really close to getting your hands on ${this._args?.planTitle ?? this._args?.plan}. Fill
        out the details below and we'll have a project ready for you when you've
        completed the purchase.
      </p>
      <uui-form>
        <form
          id="uaas-purchase-flow-form"
          name="uaas-purchase-flow-form"
          @submit=${this.#submitForm}
        >
          ${this.#renderInput("Name")}${this.#renderInput(
            "Email"
          )}${this.#renderInput("Password")}

          <div id="form-footer">
            <uui-checkbox
              id="consent"
              required
              .checked=${this._form.consent}
              @change=${this.#setFormValue}
            >
              <span>
                I agree to the
                <a
                  href="/products/umbraco-cloud/terms-and-conditions/"
                  target="_blank"
                  class="link is-blue is-normal-font is-underlined"
                  >terms and conditions</a
                >
                and
                <a
                  href="/products/umbraco-cloud/data-processing-agreement/"
                  target="_blank"
                  class="link is-blue is-normal-font is-underlined"
                  >Data Processing Agreement</a
                >
                of Umbraco.</span
              >
            </uui-checkbox>
            <uui-button
              look="primary"
              color="positive"
              type="submit"
              ?disabled=${this.#disableFormSubmit()}
              >Next</uui-button
            >
          </div>
        </form>
      </uui-form>`;
  }

  #renderStep1() {
    return html`<uui-progress-bar
        progress=${this._progress.percentage}
      ></uui-progress-bar>
      <p>${this._progress.message}</p>`;
  }

  #renderStep99() {
    return html`${when(
      this._log.description,
      () => html`<p class="lead">${this._log.description}</p>
        <p class="lead">
          <uui-button @click=${this.#reset} look="primary" color="default"
            >Try again</uui-button
          >
        </p>`,
      () => html`<dc-uaas-purchase-logger
        .user=${this._form}
        .project=${this._args}
        .log=${this._log}
      ></dc-uaas-purchase-logger>`
    )} `;
  }

  static styles = [
    ...DcDialogBaseElement.styles,
    css`
      uui-input {
        width: 100%;
      }

      #form-footer {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--unit-lg);
      }

      #form-footer span {
        line-height: 1.3;
        font-size: var(--unit-sm);
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagMap {
    [elementName]: DcPurchaseFlowDialogElement;
  }
}
