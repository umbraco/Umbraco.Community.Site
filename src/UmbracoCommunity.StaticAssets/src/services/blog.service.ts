import { ServiceBase } from "./service-base";

export interface BlogPost {
  title: string;
  url: string;
  teaser?: string;
  publishDate: string;
  readTime: number;
  imageUrl?: string;
}

export interface BlogCategory {
  name: string;
}

export interface BlogPostsResponse {
  posts: BlogPost[];
  currentPage: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
  activeTag?: string;
  activeCategory?: string;
  categories: BlogCategory[];
  tags: string[];
}

export interface BlogPostsParams {
  blogKey: string;
  page?: number;
  pageSize?: number;
  tag?: string;
  category?: string;
}

export class BlogService extends ServiceBase {
  private static readonly BASE_URL = "/api/blog";

  static async getPosts(params: BlogPostsParams): Promise<BlogPostsResponse> {
    const searchParams = new URLSearchParams();

    searchParams.set("page", (params.page ?? 1).toString());
    searchParams.set("pageSize", (params.pageSize ?? 10).toString());

    if (params.tag) {
      searchParams.append("tag", params.tag);
    }

    if (params.category) {
      searchParams.append("category", params.category);
    }

    const response = await ServiceBase.get(
      `${this.BASE_URL}/posts/${params.blogKey}?${searchParams.toString()}`
    );

    if (!response.ok) {
      throw new Error(`Failed to fetch blog posts: ${response.statusText}`);
    }

    return response.json();
  }
}
