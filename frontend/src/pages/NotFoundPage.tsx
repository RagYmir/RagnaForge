import { PageHeader } from "../components/PageHeader";

export function NotFoundPage() {
  return (
    <section className="page">
      <PageHeader
        title="Pagina nao encontrada"
        description="Use a navegacao lateral para acessar apenas as areas seguras expostas pela API."
      />
    </section>
  );
}
