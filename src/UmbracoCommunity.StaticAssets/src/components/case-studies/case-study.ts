import { css, html, LitElement } from "lit";
import { customElement, property } from "lit/decorators.js";
import { ifDefined } from "lit/directives/if-defined.js";
import { when } from "lit/directives/when.js";
import { FilterableElement } from "../filters/filterable-element.element.js";
import { repeat } from "lit/directives/repeat.js";

const elementName = "dc-case-study";

@customElement(elementName)
export class CaseStudyElement extends FilterableElement {
  @property({ attribute: "link-text" })
  linkText?: string;

  @property()
  link?: string;

  @property({ type: Array })
  type?: Array<string> = [];

  @property({ type: Array })
  skill?: Array<string> = [];

  @property({ type: Array })
  sector?: Array<string> = [];

  @property()
  country?: string;

  @property()
  partner?: string;

  @property({ attribute: "is-featured" })
  isFeatured: boolean = false;

  render() {
    return html`<a href=${ifDefined(this.link)}
      >
      <slot name="logo"></slot>

      <div id="inner">
        <div id="image">
          <slot name="thumbnail"></slot>
        </div>

        ${when(this.isFeatured, () => html`<div id="featured"><uui-tag>Featured</uui-tag></div>`)}

        <div id="content">
          <div id="meta">
            ${when(this.partner, () => html`<uui-tag>${this.partner}</uui-tag>`)}
            ${when(this.partner && this.country, () => html`&nbsp;|&nbsp;`)}
            ${when(this.country, () => html`<uui-tag>${this.country}</uui-tag>`)}
          </div>
          <div id="description">
            <slot name="name"></slot>
            <div id="skill">
              ${when(this.skill, () => html`
                ${repeat(this.skill!.slice(0, 3), (skill) => html`<uui-tag>${skill}</uui-tag>`)}
                ${when(this.skill!.length > 3, () => html`<uui-tag class="remaining">+${this.skill!.length - 3}</uui-tag>`)}
              `)}
            </div>
            <slot name="teaser"></slot>
          </div>
        </div>
      </div>
      <div id="button" aria-hidden="true">
        <svg width="34" height="34" viewBox="0 0 22 22" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true"><g><path id="Vector" d="M6.41675 6.41663H15.5834V15.5833" stroke="#283A97" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"></path><path id="Vector_2" d="M6.41675 15.5833L15.5834 6.41663" stroke="#283A97" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"></path></g></svg>
      </div>
    </a>`;
  }

  static styles = css`
    :host {
      position: relative;
      display: flex;
      flex-direction: column;
      flex: 0 1 33%;

      --img-transform: scale(1);
      --header-color: var(--color-blue);
      --padding: var(--unit);
    }

    :host(:hover) {
      --img-transform: scale(1.05);
      --header-color: var(--color-blue);

      #button {
        background-color: var(--color-blue);

        svg {
          transform: rotate(45deg);

          path {
            stroke: #fff;
          }
        }
      }
    }

    :host([is-featured]) {
      a {
        height: 500px;
      }

      a,
      ::slotted(h3),
      #meta,
      #skill uui-tag {
        color: #fff;
      }

      #inner {
        clip-path: polygon(calc(100% - 20px) 0px, calc(100% - 20px) 0px, calc(100% - 16.755822px) 0.26176772px, calc(100% - 13.678336px) 1.01961856px, calc(100% - 10.808714px) 2.23237404px, calc(100% - 8.1881279999999px) 3.85885568px, calc(100% - 5.85775px) 5.857885px, calc(100% - 3.858752px) 8.18828352px, calc(100% - 2.2323060000001px) 10.80887276px, calc(100% - 1.019584px) 13.67847424px, calc(100% - 0.26175800000004px) 16.75590948px, calc(100% - 5.6843418860808E-14px) 20px, calc(100% - 0px) calc(100% - 90.747px), calc(100% - 0px) calc(100% - 90.747px), calc(100% - 0.26175799999993px) calc(100% - 87.503065px), calc(100% - 1.0195839999999px) calc(100% - 84.42572px), calc(100% - 2.2323060000001px) calc(100% - 81.556155px), calc(100% - 3.858752px) calc(100% - 78.93556px), calc(100% - 5.85775px) calc(100% - 76.605125px), calc(100% - 8.1881279999999px) calc(100% - 74.60604px), calc(100% - 10.808714px) calc(100% - 72.979495px), calc(100% - 13.678336px) calc(100% - 71.76668px), calc(100% - 16.755822px) calc(100% - 71.008785px), calc(100% - 20px) calc(100% - 70.747px), calc(100% - 31.141px) calc(100% - 70.747px), calc(100% - 31.141px) calc(100% - 70.747px), calc(100% - 37.629113px) calc(100% - 70.223484px), calc(100% - 43.783944px) calc(100% - 68.707832px), calc(100% - 49.523131px) calc(100% - 66.282388px), calc(100% - 54.764312px) calc(100% - 63.029496px), calc(100% - 59.425125px) calc(100% - 59.0315px), calc(100% - 63.423208px) calc(100% - 54.370744px), calc(100% - 66.676199px) calc(100% - 49.129572px), calc(100% - 69.101736px) calc(100% - 43.390328px), calc(100% - 70.617457px) calc(100% - 37.235356px), calc(100% - 71.141px) calc(100% - 30.747px), calc(100% - 71.141px) calc(100% - 20px), calc(100% - 71.141px) calc(100% - 20px), calc(100% - 71.402758px) calc(100% - 16.756065px), calc(100% - 72.160584px) calc(100% - 13.67872px), calc(100% - 73.373306px) calc(100% - 10.809155px), calc(100% - 74.999752px) calc(100% - 8.1885599999999px), calc(100% - 76.99875px) calc(100% - 5.858125px), calc(100% - 79.329128px) calc(100% - 3.85904px), calc(100% - 81.949714px) calc(100% - 2.232495px), calc(100% - 84.819336px) calc(100% - 1.0196800000001px), calc(100% - 87.896822px) calc(100% - 0.26178500000003px), calc(100% - 91.141px) calc(100% - 0px), 20px calc(100% - 0px), 20px calc(100% - 0px), 16.75590948px calc(100% - 0.26202799999993px), 13.67847424px calc(100% - 1.0200639999999px), 10.80887276px calc(100% - 2.2329360000001px), 8.18828352px calc(100% - 3.859472px), 5.857885px calc(100% - 5.8585px), 3.85885568px calc(100% - 8.188848px), 2.23237404px calc(100% - 10.809344px), 1.01961856px calc(100% - 13.678816px), 0.26176772px calc(100% - 16.756092px), 3.3111302509101E-31px calc(100% - 20px), 0px 90.7471px, 0px 90.7471px, 0.26176772px 87.5029949px, 1.01961856px 84.4255512px, 2.23237404px 81.5559463px, 3.85885568px 78.9353576px, 5.857885px 76.6049625px, 8.18828352px 74.6059384px, 10.80887276px 72.9794627px, 13.67847424px 71.7667128px, 16.75590948px 71.0088661px, 20px 70.7471px, 31.1406px 70.7471px, 31.1406px 70.7471px, 37.6288102px 70.2235408px, 43.7836976px 68.7078264px, 49.5229074px 66.2823116px, 54.7640848px 63.0293512px, 59.424875px 59.0313px, 63.4229232px 54.3705128px, 66.6758746px 49.1293444px, 69.1013744px 43.3901496px, 70.6170678px 37.2352832px, 71.1406px 30.7471px, 71.1406px 20px, 71.1406px 20px, 71.4023661px 16.75589733px, 72.1602128px 13.67845504px, 73.3729627px 10.80885071px, 74.9994384px 8.18826192px, 76.9984625px 5.85786625px, 79.3288576px 3.85884128px, 81.9494463px 2.23236459px, 84.8190512px 1.01961376px, 87.8964949px 0.26176637px, 91.1406px 3.3111117619826E-31px, calc(100% - 20px) 0px);
        z-index: 0;
        height: 100%;

        #image,
        [name="thumbnail"] {
          height: 100%;
        }

        [name="thumbnail"]::slotted(img) {
          height: 100% !important;
          object-fit: cover;
        }
      }

      #content {
        position: absolute;
        top: 0;
        right: 0;
        bottom: 0;
        left: 0;
        z-index: 100;
        display: flex;
        flex-direction: column;
        justify-content: flex-end;
        background: linear-gradient(180deg, rgba(0, 0, 0, 0.2) 0.65%, rgba(0, 0, 0, 0.8) 100%);

        #description {
          slot[name="teaser"] {
            display: none;
          }
        }
      }

      #image,
      #content {
        clip-path: none;
      }
    }

    :host([is-featured]) #content {
      padding: var(--unit);
    }

    @media (min-width: 768px) {
      :host([is-featured]) a {
        height: 100%;
      }

      :host([is-featured]) #inner {
        clip-path: polygon(calc(100% - 20px) 0px, calc(100% - 20px) 0px, calc(100% - 16.755822px) 0.26176664px, calc(100% - 13.678336px) 1.01961472px, calc(100% - 10.808714px) 2.23236648px, calc(100% - 8.188128px) 3.85884416px, calc(100% - 5.85775px) 5.85787px, calc(100% - 3.858752px) 8.18826624px, calc(100% - 2.232306px) 10.80885512px, calc(100% - 1.0195839999999px) 13.67845888px, calc(100% - 0.26175799999999px) 16.75589976px, calc(100% - 1.1368683772162E-13px) 20px, calc(100% - 0px) calc(100% - 114.25px), calc(100% - 0px) calc(100% - 114.25px), calc(100% - 0.26175799999987px) calc(100% - 111.005822px), calc(100% - 1.0195839999999px) calc(100% - 107.928336px), calc(100% - 2.232306px) calc(100% - 105.058714px), calc(100% - 3.8587520000001px) calc(100% - 102.438128px), calc(100% - 5.85775px) calc(100% - 100.10775px), calc(100% - 8.188128px) calc(100% - 98.108752px), calc(100% - 10.808714px) calc(100% - 96.482306px), calc(100% - 13.678336px) calc(100% - 95.269584px), calc(100% - 16.755822px) calc(100% - 94.511758px), calc(100% - 20px) calc(100% - 94.25px), calc(100% - 53.499px) calc(100% - 94.25px), calc(100% - 53.499px) calc(100% - 94.25px), calc(100% - 59.987113px) calc(100% - 93.726457px), calc(100% - 66.141944px) calc(100% - 92.210736px), calc(100% - 71.881131px) calc(100% - 89.785199px), calc(100% - 77.122312px) calc(100% - 86.532208px), calc(100% - 81.783125px) calc(100% - 82.534125px), calc(100% - 85.781208px) calc(100% - 77.873312px), calc(100% - 89.034199px) calc(100% - 72.632131px), calc(100% - 91.459736px) calc(100% - 66.892944px), calc(100% - 92.975457px) calc(100% - 60.738113px), calc(100% - 93.499px) calc(100% - 54.25px), calc(100% - 93.499px) calc(100% - 20px), calc(100% - 93.499px) calc(100% - 20px), calc(100% - 93.760785px) calc(100% - 16.756065px), calc(100% - 94.51868px) calc(100% - 13.67872px), calc(100% - 95.731495px) calc(100% - 10.809155px), calc(100% - 97.35804px) calc(100% - 8.1885599999999px), calc(100% - 99.357125px) calc(100% - 5.858125px), calc(100% - 101.68756px) calc(100% - 3.8590399999999px), calc(100% - 104.308155px) calc(100% - 2.232495px), calc(100% - 107.17772px) calc(100% - 1.01968px), calc(100% - 110.255065px) calc(100% - 0.26178499999997px), calc(100% - 113.499px) calc(100% - 0px), 20px calc(100% - 0.00099999999997635px), 20px calc(100% - 0.00099999999997635px), 16.75595322px calc(100% - 0.26275799999996px), 13.67854336px calc(100% - 1.0205839999999px), 10.80895214px calc(100% - 2.233306px), 8.18836128px calc(100% - 3.859752px), 5.8579525px calc(100% - 5.85875px), 3.85890752px calc(100% - 8.189128px), 2.23240806px calc(100% - 10.809714px), 1.01963584px calc(100% - 13.679336px), 0.26177258px calc(100% - 16.756822px), 3.3111968110489E-31px calc(100% - 20.001px), 0px 114px, 0px 114px, 0.2617661px 110.755822px, 1.0196128px 107.678336px, 2.2323627px 104.808714px, 3.8588384px 102.188128px, 5.8578625px 99.85775px, 8.1882576px 97.858752px, 10.8088463px 96.232306px, 13.6784512px 95.019584px, 16.7558949px 94.261758px, 20px 94px, 54px 94px, 54px 94px, 60.4882102px 93.4764678px, 66.6430976px 91.9607744px, 72.3823074px 89.5352746px, 77.6234848px 86.2823232px, 82.284275px 82.284275px, 86.2823232px 77.6234848px, 89.5352746px 72.3823074px, 91.9607744px 66.6430976px, 93.4764678px 60.4882102px, 94px 54px, 94px 20px, 94px 20px, 94.261758px 16.75589733px, 95.019584px 13.67845504px, 96.232306px 10.80885071px, 97.858752px 8.18826192px, 99.85775px 5.85786625px, 102.188128px 3.85884128px, 104.808714px 2.23236459px, 107.678336px 1.01961376px, 110.755822px 0.26176637px, 114px 3.3111117619826E-31px, calc(100% - 20px) 0px);
      }

      :host([is-featured]) #content {
        padding: 28px 18px;
      }
    }

    @media (min-width: 1024px) {
      :host([is-featured]) #content {
        padding: 28px 23px;
      }
    }

    @media (min-width: 1216px) {
      :host([is-featured]) #content {
        padding: 28px 24px;
      }
    }

    @media (min-width: 1408px) {
      :host([is-featured]) #content {
        padding: 31px 34px;
      }
    }

    [name="logo"] {
      display: none;
    }

    :host([has-logo]) [name="logo"] {
      display: block;
      height: 55px;
      width: 55px;
      position: absolute;
      z-index: 1;
      background: #fff;
      top: 0;
      left: 0;
      border-radius: 50%;
    }

    @media (min-width: 768px) {
      :host([has-logo]) [name="logo"] {
        width: 74px;
        height: 74px;
      }
    }

    [name="logo"]::slotted(img) {
      height: 100%;
      border-radius: 50%;
      /* box-shadow: var(--base-box-shadow); */
      margin: 0 !important;
    }

    ::slotted(h3) {
      color: var(--header-color);
      font-size: 35px !important;
      line-height: 40px;
      margin: 0;
    }

    [name="teaser"] {
      display: block;
      margin: var(--unit) 0 0;
      padding-right: 70px;
      font-size: 18px;
      color: var(--color-blue);
    }

    @media (min-width: 768px) {
      [name="teaser"] {
        padding-right: 92px;
      }
    }

    [name="thumbnail"] {
      display: block;
      overflow: hidden;
    }

    [name="thumbnail"]::slotted(img) {
      transform: var(--img-transform);
      transition: transform 0.2s;
      width: 100%;
    }

    #inner {
      height: 100%;
      display: flex;
      flex-direction: column;
    }

    #featured {
      position: absolute;
      top: 12px;
      right: 14px;
      padding: 5px 15px;
      font-size: 12px;
      font-weight: 600;
      color: #fff;
      text-transform: uppercase;
      border: 2px solid white;
      border-radius: 100px;
      z-index: 110;

      @media (min-width: 768px) {
        top: 18px;
        right: 18px;
        padding: 5px 18px;
      }

      @media (min-width: 1216px) {
        top: 21px;
        right: 19px;
        padding: 7px 21px;
        font-size: 14px;
      }

      @media (min-width: 1408px) {
        top: 21px;
        right: 21px;
        padding: 9px 26px;
      }
    }

    #image {
      position: relative;
      background-color: white;
      clip-path: polygon(calc(100% - 0px) calc(100% - 0px), 0px calc(100% - 0px), 0px 90.7472px, 0px 90.7472px, 0.2617661px 87.5030949px, 1.0196128px 84.4256512px, 2.2323627px 81.5560463px, 3.8588384px 78.9354576px, 5.8578625px 76.6050625px, 8.1882576px 74.6060384px, 10.8088463px 72.9795627px, 13.6784512px 71.7668128px, 16.7558949px 71.0089661px, 20px 70.7472px, 31.1405px 70.7472px, 31.1405px 70.7472px, 37.6287102px 70.2236678px, 43.7835976px 68.7079744px, 49.5228074px 66.2824746px, 54.7639848px 63.0295232px, 59.424775px 59.031475px, 63.4228232px 54.3706848px, 66.6757746px 49.1295074px, 69.1012744px 43.3902976px, 70.6169678px 37.2354102px, 71.1405px 30.7472px, 71.1405px 20px, 71.1405px 20px, 71.4022661px 16.75589733px, 72.1601128px 13.67845504px, 73.3728627px 10.80885071px, 74.9993384px 8.18826192px, 76.9983625px 5.85786625px, 79.3287576px 3.85884128px, 81.9493463px 2.23236459px, 84.8189512px 1.01961376px, 87.8963949px 0.26176637px, 91.1405px 3.3111117619826E-31px, calc(100% - 20px) 0px, calc(100% - 20px) 0px, calc(100% - 16.755822px) 0.26176637px, calc(100% - 13.678336px) 1.01961376px, calc(100% - 10.808714px) 2.23236459px, calc(100% - 8.1881279999999px) 3.85884128px, calc(100% - 5.85775px) 5.85786625px, calc(100% - 3.858752px) 8.18826192px, calc(100% - 2.2323060000001px) 10.80885071px, calc(100% - 1.019584px) 13.67845504px, calc(100% - 0.26175800000004px) 16.75589733px, calc(100% - 5.6843418860808E-14px) 20px, calc(100% - 0px) calc(100% - 0px));
    }

    @media (min-width: 768px) {
      #image {
        clip-path: polygon(calc(100% - 0px) calc(100% - 0.062000000000012px), 0px calc(100% - 0px), 0px 114.229px, 0px 114.229px, 0.26176691px 110.984822px, 1.01961568px 107.907336px, 2.23236837px 105.037714px, 3.85884704px 102.417128px, 5.85787375px 100.08675px, 8.18827056px 98.087752px, 10.80885953px 96.461306px, 13.67846272px 95.248584px, 16.75590219px 94.490758px, 20px 94.229px, 54.6939px 94.229px, 54.6939px 94.229px, 61.1821102px 93.7054678px, 67.3369976px 92.1897744px, 73.0762074px 89.7642746px, 78.3173848px 86.5113232px, 82.978175px 82.513275px, 86.9762232px 77.8524848px, 90.2291746px 72.6113074px, 92.6546744px 66.8720976px, 94.1703678px 60.7172102px, 94.6939px 54.229px, 94.6939px 20.055px, 94.6939px 20.055px, 94.9556028px 16.8112805609px, 95.7132704px 13.7341646392px, 96.9257416px 10.8648188283px, 98.5518552px 8.2444097216px, 100.55045px 5.9141039125px, 102.8803648px 3.9150679944px, 105.5004384px 2.2884685607px, 108.3695096px 1.0754722048px, 111.4464172px 0.3172455201px, 114.69px 0.0549551px, calc(100% - 20.004px) 0px, calc(100% - 20.004px) 0px, calc(100% - 16.759467px) 0.26124742244px, calc(100% - 13.681536px) 1.01871849472px, calc(100% - 10.811409px) 2.23122539628px, calc(100% - 8.190288px) 3.85758030656px, calc(100% - 5.859375px) 5.856595405px, calc(100% - 3.8598719999999px) 8.18708287104px, calc(100% - 2.2329810000001px) 10.80785488412px, calc(100% - 1.019904px) 13.67772362368px, calc(100% - 0.26184299999994px) 16.75550126916px, calc(100% - 0px) 20px, calc(100% - 0px) calc(100% - 0.062000000000012px));
      }
    }

    #content {
      flex-grow: 1;
      padding: var(--unit) var(--unit) 1px;
      background-color: white;
      clip-path: polygon(0.000207322px 0px, calc(100% - 0px) 0px, calc(100% - 0px) calc(100% - 90.747px), calc(100% - 0px) calc(100% - 90.747px), calc(100% - 0.26175799999993px) calc(100% - 87.503065px), calc(100% - 1.0195839999999px) calc(100% - 84.42572px), calc(100% - 2.2323060000001px) calc(100% - 81.556155px), calc(100% - 3.858752px) calc(100% - 78.93556px), calc(100% - 5.85775px) calc(100% - 76.605125px), calc(100% - 8.1881279999999px) calc(100% - 74.60604px), calc(100% - 10.808714px) calc(100% - 72.979495px), calc(100% - 13.678336px) calc(100% - 71.76668px), calc(100% - 16.755822px) calc(100% - 71.008785px), calc(100% - 20px) calc(100% - 70.747px), calc(100% - 31.141px) calc(100% - 70.747px), calc(100% - 31.141px) calc(100% - 70.747px), calc(100% - 37.629113px) calc(100% - 70.223484px), calc(100% - 43.783944px) calc(100% - 68.707832px), calc(100% - 49.523131px) calc(100% - 66.282388px), calc(100% - 54.764312px) calc(100% - 63.029496px), calc(100% - 59.425125px) calc(100% - 59.0315px), calc(100% - 63.423208px) calc(100% - 54.370744px), calc(100% - 66.676199px) calc(100% - 49.129572px), calc(100% - 69.101736px) calc(100% - 43.390328px), calc(100% - 70.617457px) calc(100% - 37.235356px), calc(100% - 71.141px) calc(100% - 30.747px), calc(100% - 71.141px) calc(100% - 20px), calc(100% - 71.141px) calc(100% - 20px), calc(100% - 71.402758px) calc(100% - 16.755822px), calc(100% - 72.160584px) calc(100% - 13.678336px), calc(100% - 73.373306px) calc(100% - 10.808714px), calc(100% - 74.999752px) calc(100% - 8.188128px), calc(100% - 76.99875px) calc(100% - 5.85775px), calc(100% - 79.329128px) calc(100% - 3.858752px), calc(100% - 81.949714px) calc(100% - 2.232306px), calc(100% - 84.819336px) calc(100% - 1.019584px), calc(100% - 87.896822px) calc(100% - 0.26175799999999px), calc(100% - 91.141px) calc(100% - 5.6843418860808E-14px), 20.0002px calc(100% - 0px), 20.0002px calc(100% - 0px), 16.75609444983px calc(100% - 0.26175799999999px), 13.67864953208px calc(100% - 1.019584px), 10.80904284589px calc(100% - 2.232306px), 8.1884519904px calc(100% - 3.858752px), 5.85805456475px calc(100% - 5.85775px), 3.85902816808px calc(100% - 8.188128px), 2.23255039953px calc(100% - 10.808714px), 1.01979885824px calc(100% - 13.678336px), 0.26195114335px calc(100% - 16.755822px), 0.000184854px calc(100% - 20px), 0.000207322px 0px);
    }

    @media (min-width: 768px) {
      #content {
        clip-path: polygon(-0.00192287px 0px, calc(100% - 0px) 0px, calc(100% - 0px) calc(100% - 114.17px), calc(100% - 0px) calc(100% - 114.17px), calc(100% - 0.26178599999997px) calc(100% - 110.925822px), calc(100% - 1.0196879999999px) calc(100% - 107.848336px), calc(100% - 2.232522px) calc(100% - 104.978714px), calc(100% - 3.859104px) calc(100% - 102.358128px), calc(100% - 5.85825px) calc(100% - 100.02775px), calc(100% - 8.188776px) calc(100% - 98.028752px), calc(100% - 10.809498px) calc(100% - 96.402306px), calc(100% - 13.679232px) calc(100% - 95.189584px), calc(100% - 16.756794px) calc(100% - 94.431758px), calc(100% - 20.001px) calc(100% - 94.17px), calc(100% - 54.694px) calc(100% - 94.17px), calc(100% - 54.694px) calc(100% - 94.17px), calc(100% - 61.182356px) calc(100% - 93.646457px), calc(100% - 67.337328px) calc(100% - 92.130736px), calc(100% - 73.076572px) calc(100% - 89.705199px), calc(100% - 78.317744px) calc(100% - 86.452208px), calc(100% - 82.9785px) calc(100% - 82.454125px), calc(100% - 86.976496px) calc(100% - 77.793312px), calc(100% - 90.229388px) calc(100% - 72.552131px), calc(100% - 92.654832px) calc(100% - 66.812944px), calc(100% - 94.170484px) calc(100% - 60.658113px), calc(100% - 94.694px) calc(100% - 54.17px), calc(100% - 94.694px) calc(100% - 20px), calc(100% - 94.694px) calc(100% - 20px), calc(100% - 94.955785px) calc(100% - 16.755822px), calc(100% - 95.71368px) calc(100% - 13.678336px), calc(100% - 96.926495px) calc(100% - 10.808714px), calc(100% - 98.55304px) calc(100% - 8.188128px), calc(100% - 100.552125px) calc(100% - 5.85775px), calc(100% - 102.88256px) calc(100% - 3.858752px), calc(100% - 105.503155px) calc(100% - 2.232306px), calc(100% - 108.37272px) calc(100% - 1.019584px), calc(100% - 111.450065px) calc(100% - 0.26175799999999px), calc(100% - 114.694px) calc(100% - 5.6843418860808E-14px), 19.998px calc(100% - 0px), 19.998px calc(100% - 0px), 16.75391081544px calc(100% - 0.26175799999993px), 13.67647920432px calc(100% - 1.0195839999999px), 10.80688308048px calc(100% - 2.232306px), 8.18630035776px calc(100% - 3.858752px), 5.85590895px calc(100% - 5.85775px), 3.85688677104px calc(100% - 8.188128px), 2.23041173472px calc(100% - 10.808714px), 1.01766175488px calc(100% - 13.678336px), 0.25981474536px calc(100% - 16.755822px), -0.00195138px calc(100% - 20px), -0.00192287px 0px);
      }
    }

    @media (min-width: 768px) {
      :host {
        --padding: var(--unit-md);
      }

      #content {
        display: flex;
        flex-direction: column;
        justify-content: flex-start;
      }
    }

    #description {
      overflow-wrap: break-word;
    }

    #meta {
      margin-bottom: var(--unit-sm);
      font-size: 12px;
      font-weight: 500;
      color: var(--color-blue);
      text-transform: uppercase;
    }

    #meta p {
      color: var(--color-blue);
      font-weight: bold;
      margin: 0;
    }

    #skill {
      display: flex;
      flex-wrap: wrap;
      gap: 3px;
      margin-top: var(--unit);

      uui-tag {
        display: inline-flex;
        justify-content: center;
        align-items: center;
        border: 1px solid var(--color-light-grey);
        border-radius: 100px;
        padding: 2px 8px;
        font-size: 10px;
        color: var(--color-blue);

        &.remaining {
          width: 23px;
          height: 23px;
          padding: 0;
          display: inline-flex;
          justify-content: center;
          align-items: center;
        }
      }
    }

    @media (min-width: 1216px) {
      #skill uui-tag {
        font-size: 12px;
      }
    }

    #button {
      position: absolute;
      display: flex;
      justify-content: center;
      align-items: center;
      bottom: 0;
      right: 0;
      width: 55px;
      height: 55px;
      background-color: #fff;
      border-radius: 50%;
      transition: all ease-out 0.2s;

      svg {
        transition: all ease-out 0.2s;

        path {
          transition: all ease-out 0.2s;
        }
      }
    }

    @media (min-width: 768px) {
      #button {
        width: 74px;
        height: 74px;
      }
    }

    a {
      text-decoration: none;
      color: var(--color-dark);
      height: 100%;
      display:flex;
      flex-direction:column;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: CaseStudyElement;
  }
}
