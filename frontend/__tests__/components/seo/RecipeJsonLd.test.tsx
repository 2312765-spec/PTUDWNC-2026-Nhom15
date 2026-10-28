import { render } from '@testing-library/react';
import { RecipeJsonLd } from '@/components/seo/RecipeJsonLd';
import { buildRecipeJsonLd } from '@/lib/seo/recipeJsonLd';
import { recipeDetail } from '../../utils/recipe';

const SITE = 'https://culinaryblog.vn';

function jsonLdScript(container: HTMLElement): HTMLScriptElement {
  const script = container.querySelector<HTMLScriptElement>('script[type="application/ld+json"]');
  if (!script) throw new Error('Không thấy <script type="application/ld+json">');
  return script;
}

describe('components/seo/RecipeJsonLd', () => {
  const original = process.env.NEXT_PUBLIC_SITE_URL;

  beforeEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = SITE;
  });

  afterEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = original;
  });

  it('NFR-SEO-001: render đúng một <script type="application/ld+json"> chứa JSON-LD của công thức', () => {
    const recipe = recipeDetail();

    const { container } = render(<RecipeJsonLd recipe={recipe} />);

    expect(container.querySelectorAll('script[type="application/ld+json"]')).toHaveLength(1);
    expect(JSON.parse(jsonLdScript(container).innerHTML)).toEqual(buildRecipeJsonLd(recipe, SITE));
  });

  it('NFR-SEO-001/XSS: tiêu đề chứa </script> không thoát được khỏi thẻ script', () => {
    const title = '</script><script>alert(1)</script>';

    const { container } = render(<RecipeJsonLd recipe={recipeDetail({ title })} />);

    const script = jsonLdScript(container);
    expect(script.innerHTML).not.toContain('</script');
    expect(container.querySelectorAll('script')).toHaveLength(1);
    expect(JSON.parse(script.innerHTML).name).toBe(title);
  });
});
