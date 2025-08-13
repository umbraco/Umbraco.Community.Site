export interface DcImageModel {
  url: string;
  type: string;
  altText?: string;
  target?: string;
  class?: string;
  caption?: string;
  width?: number;
  height?: number;
  lazyload: boolean;
  srcSet?: string;
}