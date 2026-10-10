import { serializeJsonLd } from '@/lib/seo/jsonLd';

describe('serializeJsonLd — NFR-SEO-001', () => {
  it('NFR-SEO-001/XSS: tiêu đề chứa </script> không đóng được thẻ script', () => {
    const output = serializeJsonLd({ name: '</script><script>alert(1)</script>' });

    expect(output).not.toContain('</script>');
    expect(output).not.toContain('<');
  });

  it('NFR-SEO-001: vẫn là JSON hợp lệ, đọc ra đúng chuỗi gốc', () => {
    const data = { name: 'Phở <bò> & "gà"', note: 'dòng\u2028mới' };

    expect(JSON.parse(serializeJsonLd(data))).toEqual(data);
  });
});
