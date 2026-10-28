/**
 * @file Unit tests cho các trang thông báo lỗi 404 (Not Found).
 * @see NFR-USE-004 (Custom Not Found UI Verification)
 */

import React from "react";
import { render, screen } from "@testing-library/react";
import RecipeNotFound from "@/app/(public)/recipes/[slug]/not-found";
import CategoryNotFound from "@/app/categories/[slug]/not-found";

describe("NFR-USE-004: 404 Not Found Pages", () => {
  describe("RecipeNotFound Component", () => {
    it("render đúng tiêu đề và thông điệp tiếng Việt thân thiện", () => {
      render(<RecipeNotFound />);
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Không tìm thấy công thức");
      expect(
        screen.getByText(/Công thức nấu ăn bạn đang tìm kiếm không tồn tại/i)
      ).toBeInTheDocument();
    });

    it("không để lộ mã lỗi nội bộ RECIPE_NOT_FOUND", () => {
      render(<RecipeNotFound />);
      expect(screen.queryByText(/RECIPE_NOT_FOUND/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/404/i)).not.toBeInTheDocument();
    });

    it("chứa link điều hướng hợp lệ về /recipes và trang chủ /", () => {
      render(<RecipeNotFound />);
      const exploreLink = screen.getByRole("link", { name: /Khám phá công thức khác/i });
      const homeLink = screen.getByRole("link", { name: /Về trang chủ/i });

      expect(exploreLink).toHaveAttribute("href", "/recipes");
      expect(homeLink).toHaveAttribute("href", "/");
    });
  });

  describe("CategoryNotFound Component", () => {
    it("render đúng tiêu đề và thông điệp tiếng Việt thân thiện", () => {
      render(<CategoryNotFound />);
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Không tìm thấy danh mục");
      expect(
        screen.getByText(/Danh mục món ăn bạn đang tìm kiếm không tồn tại/i)
      ).toBeInTheDocument();
    });

    it("không để lộ mã lỗi nội bộ CATEGORY_NOT_FOUND", () => {
      render(<CategoryNotFound />);
      expect(screen.queryByText(/CATEGORY_NOT_FOUND/i)).not.toBeInTheDocument();
    });

    it("chứa link điều hướng hợp lệ về /categories và trang chủ /", () => {
      render(<CategoryNotFound />);
      const categoriesLink = screen.getByRole("link", { name: /Xem tất cả danh mục/i });
      const homeLink = screen.getByRole("link", { name: /Về trang chủ/i });

      expect(categoriesLink).toHaveAttribute("href", "/categories");
      expect(homeLink).toHaveAttribute("href", "/");
    });
  });
});