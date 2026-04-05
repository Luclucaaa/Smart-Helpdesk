# RAG Guide - Smart Helpdesk

Tai lieu nay mo ta phien ban RAG moi da su dung Vector DB (Qdrant).

## 1. Muc tieu

- Chatbot chi tra loi trong pham vi do an Smart-Helpdesk.
- Tra loi ngan gon (toi da 3 cau).
- Truy xuat ngu canh bang vector similarity de ket qua dung nghia hon.

## 2. Luong xu ly hien tai

1. Nguoi dung goi API POST /api/ai/ask.
2. Backend tim vector chunks lien quan tu Qdrant.
3. Neu Qdrant chua san sang, backend fallback sang lexical retrieval.
4. Backend gui cau hoi + context da truy xuat sang Gemini.
5. Neu khong co context phu hop, bot tra loi ngoai pham vi.

## 3. Du lieu duoc index

Service RAG quet project root va index theo cac dieu kien:

- Dinh dang: .md, .txt, .cs, .json.
- Loai tru thu muc: bin, obj, .git, .vs, node_modules.
- Uu tien khu vuc:
  - Toan bo file .md.
  - Backend/Controllers.
  - Backend/Services.
  - Backend/Interfaces.
  - Frontend/Shared.

Moi file duoc chia thanh chunks theo line, sau do tao embedding va upsert vao Qdrant.

## 4. Cau hinh

Cau hinh tai Backend/appsettings.json:

```json
"Rag": {
  "TopK": 5,
  "MaxContextChars": 5500,
  "UseVectorDb": true,
  "EmbeddingModel": "text-embedding-004",
  "VectorDb": {
    "Url": "http://localhost:6333",
    "Collection": "smarthelpdesk_kb",
    "ApiKey": ""
  }
}
```

Y nghia:

- TopK: so chunk toi da lay tu retrieval.
- MaxContextChars: gioi han tong context dua vao prompt.
- UseVectorDb: bat/tat vector retrieval.
- EmbeddingModel: model embedding cua Gemini.
- VectorDb.Url: endpoint Qdrant.
- VectorDb.Collection: collection luu vector KB.
- VectorDb.ApiKey: dung khi Qdrant cloud can xac thuc.

## 5. Chay Qdrant local

Neu may chua co Qdrant, co the chay nhanh bang Docker:

```powershell
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

Sau khi Qdrant chay, khoi dong lai Backend de index vector.

## 6. Kiem tra nhanh

1. Hoi cau trong pham vi do an (ticket, auth, comment, product).
2. Hoi cau ngoai pham vi (vd: du bao thoi tiet) de xac nhan bot tu choi.
3. Tam tat UseVectorDb = false de doi chieu giua vector va lexical fallback.

## 7. Luu y van hanh

- Neu Qdrant loi ket noi, chatbot se fallback lexical de tranh downtime.
- Moi lan cap nhat lon tai lieu/code nen restart backend de re-index.
- Co the toi uu toc do index sau bang cache embeddings va incremental indexing.
