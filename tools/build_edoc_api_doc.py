from __future__ import annotations

import json
from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs" / "SRT-eDOC-API-Integration-Spec-v1.0.working.docx"
SAMPLE_JSON = ROOT / "docs" / "srt-edoc-document-create-uat-sample.json"

FONT = "Tahoma"
CODE_FONT = "Consolas"
NAVY = "0B2545"
BLUE = "2E74B5"
DARK_BLUE = "1F4D78"
MUTED = "5B6573"
LIGHT_BLUE = "E8EEF5"
LIGHT_GRAY = "F2F4F7"
CALLOUT = "F4F6F9"
GOLD_FILL = "FFF4CE"
GREEN_FILL = "EAF4EA"
RED_FILL = "FCE8E6"
WHITE = "FFFFFF"
BLACK = "111827"
BORDER = "C7D0DA"

CONTENT_DXA = 9360
TABLE_INDENT_DXA = 120
CELL_MARGINS = {"top": 80, "bottom": 80, "start": 120, "end": 120}


def set_run_font(run, *, name=FONT, size=None, bold=None, italic=None, color=None):
    run.font.name = name
    if run._element.rPr is None:
        run._element.get_or_add_rPr()
    rfonts = run._element.rPr.get_or_add_rFonts()
    rfonts.set(qn("w:ascii"), name)
    rfonts.set(qn("w:hAnsi"), name)
    rfonts.set(qn("w:eastAsia"), name)
    rfonts.set(qn("w:cs"), name)
    lang = run._element.rPr.find(qn("w:lang"))
    if lang is None:
        lang = OxmlElement("w:lang")
        run._element.rPr.append(lang)
    lang.set(qn("w:val"), "th-TH")
    lang.set(qn("w:eastAsia"), "th-TH")
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic
    if color is not None:
        run.font.color.rgb = RGBColor.from_string(color)
    return run


def configure_styles(doc: Document):
    styles = doc.styles
    normal = styles["Normal"]
    normal.font.name = FONT
    normal.font.size = Pt(11)
    normal.font.color.rgb = RGBColor.from_string(BLACK)
    normal._element.rPr.rFonts.set(qn("w:ascii"), FONT)
    normal._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
    normal._element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
    normal.paragraph_format.space_before = Pt(0)
    normal.paragraph_format.space_after = Pt(6)
    normal.paragraph_format.line_spacing = 1.25

    title = styles["Title"]
    title.font.name = FONT
    title.font.size = Pt(25)
    title.font.bold = True
    title.font.color.rgb = RGBColor.from_string(NAVY)
    title._element.rPr.rFonts.set(qn("w:ascii"), FONT)
    title._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
    title._element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
    title.paragraph_format.space_before = Pt(0)
    title.paragraph_format.space_after = Pt(8)

    subtitle = styles["Subtitle"]
    subtitle.font.name = FONT
    subtitle.font.size = Pt(13)
    subtitle.font.color.rgb = RGBColor.from_string(MUTED)
    subtitle._element.rPr.rFonts.set(qn("w:ascii"), FONT)
    subtitle._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
    subtitle._element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
    subtitle.paragraph_format.space_after = Pt(18)

    heading_tokens = {
        "Heading 1": (16, BLUE, 18, 10),
        "Heading 2": (13, BLUE, 14, 7),
        "Heading 3": (12, DARK_BLUE, 10, 5),
    }
    for name, (size, color, before, after) in heading_tokens.items():
        style = styles[name]
        style.font.name = FONT
        style.font.size = Pt(size)
        style.font.bold = True
        style.font.color.rgb = RGBColor.from_string(color)
        style._element.rPr.rFonts.set(qn("w:ascii"), FONT)
        style._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
        style._element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
        style.paragraph_format.space_before = Pt(before)
        style.paragraph_format.space_after = Pt(after)
        style.paragraph_format.keep_with_next = True

    for name in ("Header", "Footer"):
        style = styles[name]
        style.font.name = FONT
        style.font.size = Pt(8.5)
        style.font.color.rgb = RGBColor.from_string(MUTED)
        style._element.rPr.rFonts.set(qn("w:ascii"), FONT)
        style._element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
        style._element.rPr.rFonts.set(qn("w:eastAsia"), FONT)


def set_cell_margins(cell, margins=CELL_MARGINS):
    tc = cell._tc
    tc_pr = tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for side, value in margins.items():
        node = tc_mar.find(qn(f"w:{side}"))
        if node is None:
            node = OxmlElement(f"w:{side}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def set_cell_width(cell, width_dxa):
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_w = tc_pr.find(qn("w:tcW"))
    if tc_w is None:
        tc_w = OxmlElement("w:tcW")
        tc_pr.append(tc_w)
    tc_w.set(qn("w:w"), str(width_dxa))
    tc_w.set(qn("w:type"), "dxa")


def set_cell_shading(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)
    shd.set(qn("w:val"), "clear")


def set_table_borders(table, color=BORDER, size=6):
    tbl_pr = table._tbl.tblPr
    borders = tbl_pr.find(qn("w:tblBorders"))
    if borders is None:
        borders = OxmlElement("w:tblBorders")
        tbl_pr.append(borders)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        node = borders.find(qn(f"w:{edge}"))
        if node is None:
            node = OxmlElement(f"w:{edge}")
            borders.append(node)
        node.set(qn("w:val"), "single")
        node.set(qn("w:sz"), str(size))
        node.set(qn("w:color"), color)


def set_row_repeat(row):
    tr_pr = row._tr.get_or_add_trPr()
    header = OxmlElement("w:tblHeader")
    header.set(qn("w:val"), "true")
    tr_pr.append(header)


def prevent_row_split(row):
    tr_pr = row._tr.get_or_add_trPr()
    cant_split = OxmlElement("w:cantSplit")
    tr_pr.append(cant_split)


def apply_table_geometry(table, widths_dxa, indent_dxa=TABLE_INDENT_DXA):
    assert sum(widths_dxa) == CONTENT_DXA, (widths_dxa, sum(widths_dxa))
    table.autofit = False
    table.alignment = WD_TABLE_ALIGNMENT.LEFT
    tbl_pr = table._tbl.tblPr
    tbl_w = tbl_pr.find(qn("w:tblW"))
    if tbl_w is None:
        tbl_w = OxmlElement("w:tblW")
        tbl_pr.append(tbl_w)
    tbl_w.set(qn("w:w"), str(CONTENT_DXA))
    tbl_w.set(qn("w:type"), "dxa")
    tbl_ind = tbl_pr.find(qn("w:tblInd"))
    if tbl_ind is None:
        tbl_ind = OxmlElement("w:tblInd")
        tbl_pr.append(tbl_ind)
    tbl_ind.set(qn("w:w"), str(indent_dxa))
    tbl_ind.set(qn("w:type"), "dxa")

    grid = table._tbl.tblGrid
    for child in list(grid):
        grid.remove(child)
    for width in widths_dxa:
        col = OxmlElement("w:gridCol")
        col.set(qn("w:w"), str(width))
        grid.append(col)

    for row in table.rows:
        prevent_row_split(row)
        for idx, cell in enumerate(row.cells):
            set_cell_width(cell, widths_dxa[min(idx, len(widths_dxa) - 1)])
            set_cell_margins(cell)
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    set_table_borders(table)


def style_table_text(table, header=True, font_size=9):
    for ridx, row in enumerate(table.rows):
        for cell in row.cells:
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.space_before = Pt(0)
                paragraph.paragraph_format.space_after = Pt(2)
                paragraph.paragraph_format.line_spacing = 1.1
                for run in paragraph.runs:
                    set_run_font(run, size=font_size, color=BLACK)
                    if header and ridx == 0:
                        run.bold = True
                        run.font.color.rgb = RGBColor.from_string(NAVY)
        if header and ridx == 0:
            for cell in row.cells:
                set_cell_shading(cell, LIGHT_BLUE)
            set_row_repeat(row)


def add_table(doc, headers, rows, widths, font_size=9):
    table = doc.add_table(rows=1, cols=len(headers))
    table.style = "Table Grid"
    header_tr_pr = table.rows[0]._tr.get_or_add_trPr()
    header_flag = OxmlElement("w:tblHeader")
    header_flag.set(qn("w:val"), "true")
    header_tr_pr.append(header_flag)
    for idx, header in enumerate(headers):
        table.rows[0].cells[idx].text = header
    for data in rows:
        cells = table.add_row().cells
        for idx, value in enumerate(data):
            cells[idx].text = str(value)
    apply_table_geometry(table, widths)
    style_table_text(table, font_size=font_size)
    doc.add_paragraph().paragraph_format.space_after = Pt(1)
    return table


def add_field(paragraph, instruction):
    run = paragraph.add_run()
    fld_char_begin = OxmlElement("w:fldChar")
    fld_char_begin.set(qn("w:fldCharType"), "begin")
    instr_text = OxmlElement("w:instrText")
    instr_text.set(qn("xml:space"), "preserve")
    instr_text.text = instruction
    fld_char_sep = OxmlElement("w:fldChar")
    fld_char_sep.set(qn("w:fldCharType"), "separate")
    value = OxmlElement("w:t")
    value.text = "1"
    fld_char_end = OxmlElement("w:fldChar")
    fld_char_end.set(qn("w:fldCharType"), "end")
    run._r.extend([fld_char_begin, instr_text, fld_char_sep, value, fld_char_end])
    set_run_font(run, size=8.5, color=MUTED)


def configure_page(doc):
    for section in doc.sections:
        section.page_width = Inches(8.5)
        section.page_height = Inches(11)
        section.top_margin = Inches(1)
        section.bottom_margin = Inches(1)
        section.left_margin = Inches(1)
        section.right_margin = Inches(1)
        section.header_distance = Inches(0.492)
        section.footer_distance = Inches(0.492)


def set_header_footer(section, first=False):
    header = section.header
    hp = header.paragraphs[0]
    hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    hp.paragraph_format.space_after = Pt(0)
    if not first:
        set_run_font(hp.add_run("SRT Complaint - eDOC API Integration Specification"), size=8.5, color=MUTED)

    footer = section.footer
    fp = footer.paragraphs[0]
    fp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    fp.paragraph_format.space_before = Pt(0)
    set_run_font(fp.add_run("เอกสารสำหรับ DEV/UAT | ฉบับ 1.0 | หน้า "), size=8.5, color=MUTED)
    add_field(fp, "PAGE")


def add_paragraph(doc, text="", *, bold_prefix=None, color=None, after=6, align=None, italic=False):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(after)
    p.paragraph_format.line_spacing = 1.25
    if align is not None:
        p.alignment = align
    if bold_prefix and text.startswith(bold_prefix):
        set_run_font(p.add_run(bold_prefix), bold=True, color=color or BLACK)
        set_run_font(p.add_run(text[len(bold_prefix):]), italic=italic, color=color or BLACK)
    else:
        set_run_font(p.add_run(text), italic=italic, color=color or BLACK)
    return p


def add_callout(doc, title, text, fill=CALLOUT, title_color=NAVY):
    table = doc.add_table(rows=1, cols=1)
    cell = table.cell(0, 0)
    cell.text = ""
    set_cell_shading(cell, fill)
    p = cell.paragraphs[0]
    p.paragraph_format.space_after = Pt(3)
    set_run_font(p.add_run(title), bold=True, color=title_color, size=10.5)
    p2 = cell.add_paragraph()
    p2.paragraph_format.space_after = Pt(0)
    p2.paragraph_format.line_spacing = 1.2
    set_run_font(p2.add_run(text), size=10.5, color=BLACK)
    apply_table_geometry(table, [CONTENT_DXA])
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return table


def ensure_numbering(doc):
    numbering = doc.part.numbering_part.element
    existing_abs = [int(x.get(qn("w:abstractNumId"))) for x in numbering.findall(qn("w:abstractNum"))]
    existing_num = [int(x.get(qn("w:numId"))) for x in numbering.findall(qn("w:num"))]
    base_abs = max(existing_abs or [0]) + 1
    base_num = max(existing_num or [0]) + 1

    def make_abstract(abs_id, num_fmt, text, font=None):
        abstract = OxmlElement("w:abstractNum")
        abstract.set(qn("w:abstractNumId"), str(abs_id))
        multi = OxmlElement("w:multiLevelType")
        multi.set(qn("w:val"), "singleLevel")
        abstract.append(multi)
        lvl = OxmlElement("w:lvl")
        lvl.set(qn("w:ilvl"), "0")
        start = OxmlElement("w:start")
        start.set(qn("w:val"), "1")
        fmt = OxmlElement("w:numFmt")
        fmt.set(qn("w:val"), num_fmt)
        lvl_text = OxmlElement("w:lvlText")
        lvl_text.set(qn("w:val"), text)
        suff = OxmlElement("w:suff")
        suff.set(qn("w:val"), "tab")
        ppr = OxmlElement("w:pPr")
        tabs = OxmlElement("w:tabs")
        tab = OxmlElement("w:tab")
        tab.set(qn("w:val"), "num")
        tab.set(qn("w:pos"), "540")
        tabs.append(tab)
        ind = OxmlElement("w:ind")
        ind.set(qn("w:left"), "540")
        ind.set(qn("w:hanging"), "270")
        spacing = OxmlElement("w:spacing")
        spacing.set(qn("w:after"), "80")
        spacing.set(qn("w:line"), "300")
        spacing.set(qn("w:lineRule"), "auto")
        ppr.extend([tabs, ind, spacing])
        lvl.extend([start, fmt, lvl_text, suff, ppr])
        if font:
            rpr = OxmlElement("w:rPr")
            rfonts = OxmlElement("w:rFonts")
            rfonts.set(qn("w:ascii"), font)
            rfonts.set(qn("w:hAnsi"), font)
            rpr.append(rfonts)
            lvl.append(rpr)
        abstract.append(lvl)
        numbering.append(abstract)

    def make_num(num_id, abs_id):
        num = OxmlElement("w:num")
        num.set(qn("w:numId"), str(num_id))
        abs_ref = OxmlElement("w:abstractNumId")
        abs_ref.set(qn("w:val"), str(abs_id))
        num.append(abs_ref)
        numbering.append(num)

    # Word requires all abstractNum elements to precede num elements.
    # Keeping those groups ordered also prevents the two list definitions
    # from being merged into one continued decimal sequence.
    make_abstract(base_abs, "bullet", "•", FONT)
    make_abstract(base_abs + 1, "decimal", "%1.")
    make_num(base_num, base_abs)
    make_num(base_num + 1, base_abs + 1)
    return base_num, base_num + 1


def add_list_item(doc, text, num_id):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing = 1.25
    p_pr = p._p.get_or_add_pPr()
    num_pr = OxmlElement("w:numPr")
    ilvl = OxmlElement("w:ilvl")
    ilvl.set(qn("w:val"), "0")
    num = OxmlElement("w:numId")
    num.set(qn("w:val"), str(num_id))
    num_pr.extend([ilvl, num])
    p_pr.append(num_pr)
    set_run_font(p.add_run(text), size=11)
    return p


def add_code_block(doc, text):
    table = doc.add_table(rows=1, cols=1)
    cell = table.cell(0, 0)
    cell.text = ""
    set_cell_shading(cell, LIGHT_GRAY)
    p = cell.paragraphs[0]
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.line_spacing = 1.0
    for idx, line in enumerate(text.rstrip().splitlines()):
        if idx:
            p.add_run().add_break()
        set_run_font(p.add_run(line), name=CODE_FONT, size=8.2, color=BLACK)
    apply_table_geometry(table, [CONTENT_DXA])
    doc.add_paragraph().paragraph_format.space_after = Pt(2)


def add_page_break(doc):
    doc.add_page_break()


def add_cover(doc):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(40)
    p.paragraph_format.space_after = Pt(8)
    set_run_font(p.add_run("API INTEGRATION SPECIFICATION"), size=10.5, bold=True, color=BLUE)

    title = doc.add_paragraph(style="Title")
    title.paragraph_format.keep_with_next = True
    set_run_font(title.add_run("การเชื่อมต่อระบบรับเรื่องร้องเรียน รฟท. กับระบบ eDOC"), size=25, bold=True, color=NAVY)

    subtitle = doc.add_paragraph(style="Subtitle")
    set_run_font(subtitle.add_run("Outbound RW Saraban API - ฉบับปรับปรุงล่าสุดสำหรับ DEV/UAT"), size=13, color=MUTED)

    add_callout(
        doc,
        "สาระสำคัญ",
        "ระบบ SRT Complaint เป็นฝ่ายเรียก API ของ eDOC เพื่อสร้างเอกสารใน Basket ที่กำหนด ผู้ใช้งานไม่ต้องเข้าสู่ระบบ eDOC ซ้ำ และสำหรับ flow นี้ eDOC ไม่จำเป็นต้องได้รับ API Key ของระบบ SRT",
        fill=LIGHT_BLUE,
    )

    metadata = [
        ("เจ้าของเอกสาร", "ระบบรับเรื่องร้องเรียนออนไลน์ การรถไฟแห่งประเทศไทย"),
        ("ผู้รับเอกสาร", "ทีมระบบ eDOC / RW Saraban API"),
        ("เวอร์ชัน", "1.0"),
        ("วันที่ปรับปรุง", "27 ตุลาคม 2569 (2026-10-27)"),
        ("สถานะ", "สำหรับตรวจสอบร่วมและทดสอบ DEV/UAT"),
        ("ระดับข้อมูล", "Internal / Partner Confidential - ไม่มี Credential จริง"),
    ]
    add_table(doc, ["รายการ", "รายละเอียด"], metadata, [2500, 6860], font_size=10)

    add_paragraph(
        doc,
        "เอกสารอ้างอิง: API-RW691027V2, Postman Collection ที่ eDOC ส่งให้ และ implementation ล่าสุดของ SRT Complaint ณ วันที่จัดทำ",
        color=MUTED,
        italic=True,
        after=4,
    )
    add_paragraph(
        doc,
        "หมายเหตุ: ค่า URL Production, Credential จริง, BasketID จริง และข้อกำหนดบางฟิลด์ต้องได้รับการยืนยันจาก eDOC ก่อนเปิดใช้งาน Production",
        color="7A5A00",
        italic=True,
    )


def build_document():
    doc = Document()
    configure_page(doc)
    configure_styles(doc)
    bullet_id, number_id = ensure_numbering(doc)

    props = doc.core_properties
    props.title = "SRT Complaint - eDOC API Integration Specification"
    props.subject = "Outbound RW Saraban API integration for DEV/UAT"
    props.author = ""
    props.last_modified_by = ""
    props.keywords = "SRT, eDOC, RW Saraban, API, Basket, UAT"
    props.comments = "Generated for partner integration review; contains no live credentials."

    add_cover(doc)
    add_page_break(doc)

    doc.add_heading("สารบัญและขอบเขตเอกสาร", level=1)
    toc_rows = [
        ("1", "วัตถุประสงค์และขอบเขต"),
        ("2", "บทบาทของ SRT และ eDOC"),
        ("3", "ภาพรวมการทำงานและ Basket"),
        ("4", "ข้อมูลเชื่อมต่อและ Authentication"),
        ("5", "รายการ API Endpoint"),
        ("6", "ตารางตัวแปร Configuration ของระบบ SRT"),
        ("7", "ตาราง Mapping ตัวแปร DocumentCreateOutside"),
        ("8", "Mapping การแนบไฟล์ สอบถาม และติดตามสถานะ"),
        ("9", "ตัวอย่าง JSON Request/Response"),
        ("10", "แผนทดสอบ DEV/UAT"),
        ("11", "ความปลอดภัยและการส่งมอบ Credential"),
        ("12", "รายการข้อมูลที่ขอให้ eDOC ยืนยัน"),
    ]
    add_table(doc, ["หัวข้อ", "เนื้อหา"], toc_rows, [1200, 8160], font_size=10)
    add_callout(
        doc,
        "ขอบเขตของเอกสารฉบับนี้",
        "ครอบคลุมเฉพาะ API ที่ระบบ SRT Complaint เรียกออกไปยัง eDOC ได้แก่ Login, Create, Upload, Inquiry และ Tracking ไม่ครอบคลุม Partner API v1 ที่ระบบภายนอกใช้เรียกเข้าระบบ SRT",
        fill=GOLD_FILL,
        title_color="7A5A00",
    )

    doc.add_heading("1. วัตถุประสงค์และขอบเขต", level=1)
    add_paragraph(doc, "เอกสารนี้ใช้เป็นข้อกำหนดร่วมระหว่างทีม SRT Complaint และทีม eDOC สำหรับเตรียมการเชื่อมต่อ ทดสอบ DEV/UAT และยืนยันข้อมูลก่อนเปิดใช้งาน Production")
    for item in [
        "ให้เจ้าหน้าที่เลือกฝ่าย/สำนักงาน (Basket) และกดสร้างเอกสาร eDOC จากหน้า Case Detail ได้ทันที",
        "ให้ระบบ SRT ใช้ service account เรียก API เบื้องหลัง ผู้ใช้งานไม่ต้องกรอก Username/Password ของ eDOC",
        "ให้เอกสารถูกสร้างใน Basket ที่เลือก พร้อมบันทึก WID เลขทะเบียน วันที่เอกสาร และสถานะกลับมาในระบบ SRT",
        "รองรับการส่งไฟล์แนบ การตรวจสอบเอกสาร และการติดตามสถานะโดยไม่สร้างเอกสารซ้ำโดยไม่จำเป็น",
        "กำหนดวิธีแลกเปลี่ยน Credential และข้อมูลทดสอบอย่างปลอดภัย",
    ]:
        add_list_item(doc, item, bullet_id)

    doc.add_heading("2. บทบาทและข้อมูลที่แต่ละฝ่ายต้องจัดเตรียม", level=1)
    responsibility_rows = [
        ("SRT", "Public outbound IP", "แจ้งให้ eDOC ใช้ทำ allowlist หากกำหนด"),
        ("SRT", "รายชื่อฝ่าย/สำนักงาน", "ระบุ Basket ที่ต้องการทดสอบและ category ที่เกี่ยวข้อง"),
        ("SRT", "Test WID และข้อมูล UAT", "ใช้ข้อมูลจำลอง ไม่มีข้อมูลส่วนบุคคลจริง"),
        ("SRT", "ผู้ประสานงานและช่วงเวลาทดสอบ", "ใช้สำหรับเปิดสิทธิ์ ติดตาม log และสรุปผล"),
        ("eDOC", "DEV/UAT Base URL", "ต้องเป็น HTTPS และระบุ base path ที่ถูกต้อง"),
        ("eDOC", "Service Account", "Username/Password สำหรับ API โดยเฉพาะ แยก DEV กับ Production"),
        ("eDOC", "AppId และ SarabanUsername", "ค่าที่ API ใช้ใน header/detail"),
        ("eDOC", "BasketID พร้อมชื่อหน่วยงาน", "รหัส 13 หลักของแต่ละฝ่าย/สำนักงาน"),
        ("eDOC", "Contract และข้อจำกัด", "file type/size, rate limit, error code, WID uniqueness"),
        ("eDOC", "Production endpoint", "ส่งให้หลัง UAT ผ่าน พร้อม Credential ชุดใหม่"),
    ]
    add_table(doc, ["ฝ่าย", "ข้อมูล", "วัตถุประสงค์"], responsibility_rows, [1100, 3000, 5260], font_size=9.2)
    add_callout(
        doc,
        "ไม่ต้องส่ง API Key ของ SRT ให้ eDOC ในรอบนี้",
        "Flow ปัจจุบันเป็น SRT → eDOC เท่านั้น หากอนาคต eDOC ต้อง callback หรือ webhook กลับมายัง SRT จึงค่อยออก Sandbox API Key แยกต่างหากตาม least privilege",
        fill=GREEN_FILL,
        title_color="1F5E35",
    )

    add_page_break(doc)
    doc.add_heading("3. ภาพรวมการทำงานและ Basket", level=1)
    steps = [
        "เจ้าหน้าที่เปิดเรื่องร้องเรียนที่ตนรับผิดชอบและเลือก Basket ของฝ่าย/สำนักงาน",
        "ระบบ SRT สร้าง local job พร้อม WID และล็อกหนึ่งเรื่องร้องเรียนต่อหนึ่งเอกสาร eDOC",
        "ระบบ SRT Login ไปยัง eDOC ด้วย service account และเก็บ token ใน memory ชั่วคราว",
        "ระบบเรียก DocumentCreateOutside พร้อม BasketID และข้อมูลเอกสาร",
        "เมื่อ eDOC ตอบสำเร็จ ระบบบันทึก RegisterNO/WDate ก่อนส่งไฟล์แนบ",
        "ระบบส่งไฟล์แนบทีละไฟล์ด้วย DocumentUpload และบันทึกผลรายไฟล์",
        "ระบบติดตามสถานะเป็นระยะ และผู้ใช้ตรวจสอบสถานะจากหน้า Case Detail ได้",
    ]
    for step in steps:
        add_list_item(doc, step, number_id)
    add_paragraph(doc, "Basket คือหน่วยรับเอกสารใน eDOC เอกสารจะไปปรากฏเป็นของฝ่าย/สำนักงานตาม BasketID ที่ส่งไป ระบบ SRT แสดงเฉพาะ Basket ที่ผู้ดูแลกำหนดใน allowlist และสามารถกำหนด Basket เริ่มต้นตาม Complaint Category ได้")

    basket_rows = [
        ("BasketID", "รหัสตัวเลข 13 หลัก", "eDOC", "ใช้สร้าง/ค้นหาเอกสารในหน่วยงานนั้น"),
        ("BasketName", "ชื่อฝ่าย/สำนักงาน", "eDOC/SRT Admin", "ใช้แสดงผลให้ผู้ใช้เลือก"),
        ("CategoryBasketMappings", "Category ID → BasketID", "SRT Admin", "เลือก Basket เริ่มต้นอัตโนมัติ"),
        ("Selected Basket", "Basket ที่ผู้ใช้ยืนยัน", "เจ้าหน้าที่ SRT", "บันทึกถาวรพร้อมเอกสาร เปลี่ยนหลังส่งไม่ได้"),
    ]
    add_table(doc, ["ตัวแปร", "รูปแบบ", "เจ้าของข้อมูล", "การใช้งาน"], basket_rows, [1700, 1900, 1700, 4060], font_size=9.2)

    add_page_break(doc)
    doc.add_heading("4. ข้อมูลเชื่อมต่อและ Authentication", level=1)
    environment_rows = [
        ("DEV/UAT", "https://edoc.railway.co.th/RwSarabanApiDev/", "ข้อมูลจาก Postman ที่ได้รับ", "ใช้ทดสอบหลัง eDOC ยืนยันและออก Credential ใหม่"),
        ("Production", "รอ eDOC ยืนยัน", "eDOC", "ห้ามใช้ DEV URL เมื่อ Environment=Production"),
    ]
    add_table(doc, ["Environment", "Base URL", "แหล่งข้อมูล", "สถานะ"], environment_rows, [1500, 3000, 1800, 3060], font_size=9)

    doc.add_heading("4.1 การ Login", level=2)
    add_paragraph(doc, "ระบบเรียก POST api/Authenticate/login ด้วย Username/Password ของ service account จาก secret store ค่าเหล่านี้ไม่ถูกส่งไป browser และไม่ถูกบันทึกในฐานข้อมูลหรือ log")
    add_code_block(doc, json.dumps({"username": "{{EDOC_SERVICE_USERNAME}}", "password": "{{EDOC_SERVICE_PASSWORD}}"}, ensure_ascii=False, indent=2))

    doc.add_heading("4.2 Header สำหรับ API หลัง Login", level=2)
    auth_rows = [
        ("Authorization", "Bearer {{ACCESS_TOKEN}}", "ใช้มาตรฐาน Bearer token"),
        ("access-token", "{{ACCESS_TOKEN}}", "ส่งควบคู่ตามตัวอย่าง Postman ที่ได้รับ"),
        ("Content-Type", "application/json", "Request body เป็น UTF-8 JSON"),
        ("Accept", "application/json", "ต้องการ Response เป็น JSON"),
    ]
    add_table(doc, ["Header", "ค่า", "หมายเหตุ"], auth_rows, [2000, 3000, 4360], font_size=9.2)
    add_callout(
        doc,
        "การจัดการ Token",
        "ระบบ cache token ใน memory และขอ token ใหม่เมื่อหมดอายุหรือได้รับ HTTP 401 โดยไม่แสดง token ต่อผู้ใช้และไม่ log token",
        fill=LIGHT_BLUE,
    )

    add_page_break(doc)
    doc.add_heading("5. รายการ API Endpoint", level=1)
    endpoint_rows = [
        ("Login", "POST", "api/Authenticate/login", "ขอ access token", "ไม่มี Bearer"),
        ("Create", "POST", "api/EdocDocument/DocumentCreateOutside/", "สร้างเอกสารใน Basket", "Bearer + access-token"),
        ("Upload", "POST", "api/EdocDocument/DocumentUpload/", "แนบไฟล์ Base64", "Bearer + access-token"),
        ("Inquiry", "GET + JSON body", "api/EdocDocument/DocumentInquiry/", "ตรวจว่าพบ WID ใน Basket หรือไม่", "Bearer + access-token"),
        ("Tracking", "GET + query", "api/EdocDocument/DocumentTracking/", "ติดตามสถานะจาก WID", "Bearer + access-token"),
    ]
    add_table(doc, ["ชื่อ", "Method", "Relative path", "วัตถุประสงค์", "Authentication"], endpoint_rows, [1100, 1300, 3000, 1960, 2000], font_size=8.6)
    add_callout(
        doc,
        "จุดที่ต้องทดสอบกับ Network จริง",
        "DocumentInquiry เป็น HTTP GET ที่มี JSON body ตามเอกสาร eDOC; proxy หรือ WAF บางชนิดอาจตัด body จึงต้องทดสอบผ่านเส้นทาง DEV/UAT จริง",
        fill=GOLD_FILL,
        title_color="7A5A00",
    )

    add_page_break(doc)
    doc.add_heading("6. ตารางตัวแปร Configuration ของระบบ SRT", level=1)
    add_paragraph(doc, "ตารางนี้เป็นฉบับล่าสุดตาม implementation ปัจจุบัน ตัวแปรแบบ environment variable ใช้เครื่องหมาย __ แทนระดับของ configuration")
    config_rows = [
        ("Edoc__Enabled", "SRT", "false/true", "เปิดการทำงาน eDOC; ค่าเริ่มต้น false"),
        ("Edoc__Environment", "SRT", "Development", "Development/UAT/Production; ใช้ตรวจไม่ให้ Production ชี้ ApiDev"),
        ("Edoc__BaseUrl", "eDOC", "DEV URL", "HTTPS base URL ต้องลงท้ายด้วย /"),
        ("Edoc__AllowedHosts__0", "SRT/eDOC", "edoc.railway.co.th", "Hostname allowlist ป้องกัน SSRF"),
        ("Edoc__AllowPrivateNetwork", "SRT Security", "false", "เปิดเฉพาะเมื่อ eDOC ใช้ private IP และผ่านการอนุมัติ"),
        ("Edoc__Username", "eDOC", "Secret", "Service account สำหรับ Login ห้ามใส่ใน Git"),
        ("Edoc__Password", "eDOC", "Secret", "Password สำหรับ Login ห้ามส่งในเอกสารนี้"),
        ("Edoc__AppId", "eDOC", "1", "Application identifier ความยาว 1-2 ตัวอักษร"),
        ("Edoc__SarabanUsername", "eDOC", "API user", "ส่งใน rqDetail และ tracking query"),
        ("Edoc__BasketId", "eDOC", "13 digits", "ค่า default กรณีมี Basket เดียว"),
        ("Edoc__Baskets__n__Id", "eDOC", "13 digits", "รายการ Basket allowlist ลำดับ n"),
        ("Edoc__Baskets__n__Name", "eDOC/SRT", "ชื่อหน่วยงาน", "ชื่อที่แสดงใน dropdown"),
        ("Edoc__CategoryBasketMappings__{id}", "SRT", "BasketID", "ผูก Complaint Category ID กับ Basket เริ่มต้น"),
        ("Edoc__FromName", "SRT", "ชื่อหน่วยงานต้นทาง", "ส่งเป็น From จำกัด 100 ตัวอักษร"),
        ("Edoc__DefaultSendTo", "SRT", "ชื่อหน่วยงานปลายทาง", "fallback เมื่อ Category ไม่มี DepartmentName"),
        ("Edoc__SecretLevel", "eDOC", "0", "ค่าระดับความลับ ต้องให้ eDOC ยืนยัน"),
        ("Edoc__Wstype", "eDOC", "รอยืนยัน", "ความหมาย/ค่าที่รองรับต้องให้ eDOC ยืนยัน"),
        ("Edoc__ReceiveDoc", "eDOC", "รอยืนยัน", "ความหมาย/ค่าที่รองรับต้องให้ eDOC ยืนยัน"),
        ("Edoc__UseBuddhistYear", "SRT/eDOC", "true", "true = ส่งปี พ.ศ."),
        ("Edoc__DocumentDateSeparator", "SRT/eDOC", "/", "รองรับ / หรือ -"),
        ("Edoc__MaxAttachmentBytes", "SRT/eDOC", "10485760", "ค่าเริ่มต้น 10 MB ต่อไฟล์; สูงสุดที่ระบบยอมตั้ง 20 MB"),
        ("Edoc__MaxResponseBytes", "SRT Security", "1048576", "จำกัด response ค่าเริ่มต้น 1 MB"),
        ("Edoc__TrackingIntervalMinutes", "SRT", "30", "ช่วงตรวจสถานะอัตโนมัติ 5-1440 นาที"),
    ]
    add_table(doc, ["ตัวแปรระบบ SRT", "ผู้ให้ค่า", "ตัวอย่าง", "รายละเอียด/กฎ"], config_rows, [2600, 1500, 1800, 3460], font_size=8.6)

    doc.add_heading("6.1 ตัวอย่าง Environment Variables (ไม่มี Secret จริง)", level=2)
    env_example = """Edoc__Enabled=true
Edoc__Environment=Development
Edoc__BaseUrl=https://edoc.railway.co.th/RwSarabanApiDev/
Edoc__AllowedHosts__0=edoc.railway.co.th
Edoc__Username={{EDOC_SERVICE_USERNAME}}
Edoc__Password={{EDOC_SERVICE_PASSWORD}}
Edoc__AppId=1
Edoc__SarabanUsername={{SARABAN_USERNAME}}
Edoc__Baskets__0__Id=1000000000001
Edoc__Baskets__0__Name=ฝ่ายทดสอบ A
Edoc__CategoryBasketMappings__1=1000000000001"""
    add_code_block(doc, env_example)

    add_page_break(doc)
    doc.add_heading("7. ตาราง Mapping: DocumentCreateOutside", level=1)
    add_paragraph(doc, "Endpoint: POST api/EdocDocument/DocumentCreateOutside/ โดย request มี rqHeader และ rqDetail")
    create_rows = [
        ("rqHeader.appId", "Edoc:AppId", "จำเป็น", "1", "eDOC ออกให้"),
        ("WID", "Complaint.ReferenceNumber", "จำเป็น/unique", "SRT-UAT-2569-000001", "จำกัด 100; local unique"),
        ("Username", "Edoc:SarabanUsername", "จำเป็น", "{{SARABAN_USERNAME}}", "ไม่ใช่ผู้ใช้หน้าเว็บ"),
        ("BasketID", "Basket ที่ผู้ใช้เลือก", "จำเป็น/13 digits", "1000000000001", "ต้องอยู่ใน allowlist"),
        ("RefNumber", "ExternalId หรือ ReferenceNumber", "≤100", "SRT-UAT-2569-000001", "ใช้ trace ต้นทาง"),
        ("From", "Edoc:FromName", "≤100", "งานประชาสัมพันธ์ฯ", "ชื่อหน่วยงานต้นทาง"),
        ("SendTo", "Category.DepartmentName หรือ DefaultSendTo", "≤100", "ฝ่ายทดสอบ A", "ต้องมีค่าอย่างน้อยหนึ่งแหล่ง"),
        ("Subject", "เลขเรื่อง + Category.Name", "≤255", "ทดสอบสร้างเอกสาร...", "ระบบสร้างอัตโนมัติ"),
        ("DocDate", "Complaint.CreatedAt เวลาไทย", "date string", "2569/10/27", "ปี พ.ศ./ค.ศ. ตั้งค่าได้"),
        ("Priority", "Complaint.Priority", "code", "00", "Normal/Low=00, High=01, Urgent=02, Critical=03"),
        ("SecretLevel", "Edoc:SecretLevel", "≤1", "0", "รอ eDOC ยืนยัน code"),
        ("Description", "Complaint.Description", "≤2000", "ข้อมูลทดสอบ...", "ตัดความยาวแบบ deterministic"),
        ("Remark", "ข้อความอ้างอิง WID", "≤300", "UAT...", "ใช้ช่วย trace"),
        ("Wstype", "Edoc:Wstype", "≤2", "", "รอ eDOC ยืนยัน required/value"),
        ("ReceiveDoc", "Edoc:ReceiveDoc", "≤2", "", "รอ eDOC ยืนยัน required/value"),
    ]
    add_table(doc, ["ฟิลด์ eDOC", "แหล่งข้อมูลใน SRT", "กฎ", "ตัวอย่าง", "หมายเหตุ"], create_rows, [1700, 2500, 1300, 1900, 1960], font_size=8.3)

    add_page_break(doc)
    doc.add_heading("7.1 Response ที่ระบบ SRT อ่าน", level=2)
    response_rows = [
        ("WID", "ต้องตรงกับ WID ที่ส่ง", "ถ้าไม่ตรงให้ NeedsReview เพราะผลไม่ชัดเจน"),
        ("RegisterNO", "EdocDocumentLink.RegisterNumber", "เลขทะเบียนที่แสดงใน Case Detail"),
        ("WDate", "EdocDocumentLink.RemoteDocumentDate", "วันที่เอกสารจาก eDOC"),
    ]
    add_table(doc, ["ฟิลด์ Response", "เก็บในระบบ SRT", "การตรวจสอบ"], response_rows, [1900, 3000, 4460], font_size=9)
    add_callout(
        doc,
        "การป้องกันเอกสารซ้ำ",
        "ถ้า create timeout หรือ eDOC ตอบ HTTP 5xx หลังส่ง request ระบบจะไม่ create ซ้ำอัตโนมัติ แต่เปลี่ยนเป็น NeedsReview และใช้ DocumentInquiry ตรวจ WID ก่อนให้ SuperAdmin retry",
        fill=GOLD_FILL,
        title_color="7A5A00",
    )

    add_page_break(doc)
    doc.add_heading("8. Mapping: Upload, Inquiry และ Tracking", level=1)
    doc.add_heading("8.1 DocumentUpload", level=2)
    upload_rows = [
        ("rqHeader.appId", "Edoc:AppId", "เหมือน Create"),
        ("WID", "Complaint.ReferenceNumber", "WID ของเอกสารหลัก"),
        ("Username", "Edoc:SarabanUsername", "service account identity"),
        ("BasketID", "EdocDocumentLink.BasketId", "Basket เดียวกับตอน Create"),
        ("LinkWID", "Complaint.ReferenceNumber", "อ้างอิงเอกสารหลัก"),
        ("FileName", "ชื่อไฟล์ไม่รวมนามสกุล", "ตัด path และจำกัดความยาว"),
        ("FileExtension", "นามสกุลไฟล์", "1-4 ตัวอักษร เช่น pdf, jpg, docx"),
        ("FileData", "ไฟล์ → Base64", "ไม่ log และจำกัดขนาดก่อนอ่าน"),
        ("Detail", "เอกสารแนบเรื่อง {ReferenceNumber}", "จำกัด 100 ตัวอักษร"),
    ]
    add_table(doc, ["ฟิลด์ eDOC", "แหล่งข้อมูลใน SRT", "หมายเหตุ"], upload_rows, [2000, 3300, 4060], font_size=9)
    add_paragraph(doc, "ระบบบันทึกผลรายไฟล์เป็น Pending, Uploaded หรือ NeedsReview หากเอกสารถูกสร้างแล้วแต่ไฟล์แนบบางไฟล์ล้มเหลว สถานะเอกสารจะเป็น SentWithAttachmentErrors และไม่ create เอกสารใหม่")

    doc.add_heading("8.2 DocumentInquiry", level=2)
    inquiry_rows = [
        ("Method", "GET พร้อม JSON body", "ต้องทดสอบ proxy/WAF"),
        ("WID", "EdocDocumentLink.WID", "เลขอ้างอิงที่ต้องการค้นหา"),
        ("Username", "Edoc:SarabanUsername", "service account identity"),
        ("BasketID", "EdocDocumentLink.BasketId", "Basket ที่ใช้ตอน Create"),
        ("Not found", "error001 / data not found", "ระบบตีความว่าไม่พบ WID"),
    ]
    add_table(doc, ["รายการ", "ค่า/แหล่งข้อมูล", "หมายเหตุ"], inquiry_rows, [1800, 3600, 3960], font_size=9)

    add_page_break(doc)
    doc.add_heading("8.3 DocumentTracking", level=2)
    add_paragraph(doc, "รูปแบบ URL")
    add_code_block(doc, "api/EdocDocument/DocumentTracking/?wid={WID}&usrid={SarabanUsername}&appId={AppId}")
    tracking_rows = [
        ("wid", "EdocDocumentLink.WID", "URL encoded"),
        ("usrid", "Edoc:SarabanUsername", "URL encoded"),
        ("appId", "Edoc:AppId", "URL encoded"),
        ("statusCode", "RemoteStatusCode", "ตัวอย่างเอกสารกล่าวถึง 00/02/03/09; ขอ eDOC ยืนยันความหมาย"),
        ("actionMessage", "RemoteStatusDescription", "แสดงคำอธิบายล่าสุดในระบบ SRT"),
        ("receiveDate/completeDate", "อ่านจาก response", "ใช้ประกอบการติดตามเมื่อมีค่า"),
    ]
    add_table(doc, ["ฟิลด์", "เก็บ/มาจาก", "หมายเหตุ"], tracking_rows, [2000, 3000, 4360], font_size=9)

    add_page_break(doc)
    doc.add_heading("9. ตัวอย่าง JSON Request และ Response", level=1)
    add_callout(
        doc,
        "ข้อมูลตัวอย่างเท่านั้น",
        "BasketID, Username, Password, Token และเลขทะเบียนในหัวข้อนี้เป็น placeholder/dummy ห้ามนำไปใช้ Production",
        fill=RED_FILL,
        title_color="9B1C1C",
    )

    doc.add_heading("9.1 Create Document Request", level=2)
    with SAMPLE_JSON.open("r", encoding="utf-8") as file:
        sample_create = json.load(file)
    add_code_block(doc, json.dumps(sample_create, ensure_ascii=False, indent=2))
    add_paragraph(doc, f"ไฟล์ JSON แยกสำหรับส่งให้ทีม eDOC: {SAMPLE_JSON.name}", color=MUTED, italic=True)

    doc.add_heading("9.2 ตัวอย่าง Create Response ที่ระบบรองรับ", level=2)
    create_response = {
        "WID": "SRT-UAT-2569-000001",
        "RegisterNO": "{{REGISTER_NUMBER_FROM_EDOC}}",
        "WDate": "2569/10/27",
    }
    add_code_block(doc, json.dumps(create_response, ensure_ascii=False, indent=2))
    add_paragraph(doc, "หมายเหตุ: client อ่าน key แบบ case-insensitive และค้นหาใน object wrapper ได้ แต่ขอให้ eDOC ยืนยัน response schema จริงของ DEV/UAT")

    doc.add_heading("9.3 Upload Request", level=2)
    upload_example = {
        "rqHeader": {"appId": "1"},
        "rqDetail": {
            "WID": "SRT-UAT-2569-000001",
            "Username": "{{SARABAN_USERNAME}}",
            "BasketID": "1000000000001",
            "LinkWID": "SRT-UAT-2569-000001",
            "FileName": "uat-test-document",
            "FileExtension": "pdf",
            "FileData": "{{BASE64_FILE_DATA_OMITTED}}",
            "Detail": "เอกสารแนบเรื่อง SRT-UAT-2569-000001",
        },
    }
    add_code_block(doc, json.dumps(upload_example, ensure_ascii=False, indent=2))

    doc.add_heading("9.4 Inquiry Request", level=2)
    inquiry_example = {
        "rqHeader": {"appId": "1"},
        "rqDetail": {
            "WID": "SRT-UAT-2569-000001",
            "Username": "{{SARABAN_USERNAME}}",
            "BasketID": "1000000000001",
        },
    }
    add_code_block(doc, json.dumps(inquiry_example, ensure_ascii=False, indent=2))

    doc.add_heading("9.5 Tracking Request", level=2)
    add_code_block(doc, "GET {{BASE_URL}}api/EdocDocument/DocumentTracking/?wid=SRT-UAT-2569-000001&usrid={{SARABAN_USERNAME}}&appId=1")

    add_page_break(doc)
    doc.add_heading("10. แผนทดสอบ DEV/UAT", level=1)
    uat_rows = [
        ("UAT-01", "Login สำเร็จ", "Credential DEV/UAT ถูกต้อง", "ได้ token และไม่มี secret ใน log"),
        ("UAT-02", "Create Basket A", "ใช้ WID ใหม่ + Basket A", "เอกสารปรากฏในฝ่าย A; RegisterNO ตรง"),
        ("UAT-03", "Create Basket B", "ใช้ WID ใหม่ + Basket B", "เอกสารปรากฏในฝ่าย B ไม่ผิด Basket"),
        ("UAT-04", "ภาษาไทย/วันที่", "Subject และ Description ภาษาไทย", "ไม่เป็นอักขระเพี้ยน; วันที่ตรงรูปแบบ"),
        ("UAT-05", "Priority", "ทดสอบ 00/01/02/03", "ระดับความเร่งด่วนตรงตาม eDOC"),
        ("UAT-06", "Upload PDF/JPG/DOCX", "ไฟล์ทดสอบไม่มี PII", "เปิดอ่านได้และผูก WID ถูกต้อง"),
        ("UAT-07", "Inquiry พบ WID", "สอบถาม WID ที่สร้างแล้ว", "พบ WID/เลขทะเบียนเดิม"),
        ("UAT-08", "Inquiry ไม่พบ", "ใช้ WID ที่ไม่มีจริง", "ได้ error contract ที่ตกลง"),
        ("UAT-09", "Tracking", "ติดตาม WID ที่มีสถานะ", "ได้ statusCode/actionMessage"),
        ("UAT-10", "Token หมดอายุ", "จำลอง 401", "ระบบ Login ใหม่หนึ่งครั้งและทำงานต่อ"),
        ("UAT-11", "Create timeout", "ตัด network หลังส่ง request", "ระบบ NeedsReview; Inquiry ก่อน retry; ไม่มีเอกสารซ้ำ"),
        ("UAT-12", "สิทธิ์หน้า SRT", "ผู้ใช้ที่ไม่รับผิดชอบเรื่อง", "กดสร้างไม่ได้"),
    ]
    add_table(doc, ["รหัส", "กรณีทดสอบ", "วิธีทดสอบ", "ผลที่คาดหวัง"], uat_rows, [1100, 2200, 2700, 3360], font_size=8.8)

    doc.add_heading("10.1 เกณฑ์ผ่าน UAT", level=2)
    for item in [
        "เอกสารถูกสร้างใน Basket ที่เลือกถูกต้องอย่างน้อย 2 Basket",
        "WID, RegisterNO และ WDate ที่ SRT บันทึกตรงกับ eDOC",
        "ไม่มีการขอให้ผู้ใช้ SRT Login eDOC เพิ่ม",
        "ไฟล์แนบที่รองรับเปิดอ่านได้และไม่เกินขนาดที่ตกลง",
        "timeout/retry ไม่ทำให้เกิดเอกสารซ้ำ",
        "log ไม่มี password, token, Authorization header หรือ Base64 file data",
        "eDOC และ SRT ลงนาม/ยืนยันผล UAT ก่อนขอ Production Credential",
    ]:
        add_list_item(doc, item, bullet_id)

    add_page_break(doc)
    doc.add_heading("11. ความปลอดภัยและการส่งมอบ Credential", level=1)
    security_rows = [
        ("Credential ownership", "eDOC ออก service account ให้ SRT; SRT ไม่ส่ง API Key ของตนให้ eDOC สำหรับ flow นี้"),
        ("Credential transport", "ใช้ secret vault, secure mail หรือแยก Username/Password คนละช่องทาง"),
        ("Credential storage", "เก็บใน server secret/environment variables ไม่เก็บใน Git, Word หรือ Postman ที่แชร์"),
        ("Rotation", "Credential ที่เคยอยู่ในเอกสารตัวอย่างต้อง rotate ก่อนใช้งาน"),
        ("Least privilege", "จำกัด method/Basket/source IP เท่าที่จำเป็น"),
        ("Network", "HTTPS เท่านั้น; hostname allowlist; ปิด redirect/proxy; block private IP โดย default"),
        ("Logging", "ไม่ log password/token/body/Base64; เก็บเพียง WID, HTTP status และ remote code ที่จำเป็น"),
        ("Data minimization", "UAT ใช้ข้อมูลจำลอง ไม่มีชื่อ/โทรศัพท์/เลขบัตรประชาชนจริง"),
        ("Incident", "หาก credential หลุด ให้ revoke/rotate และตรวจ audit log ทั้งสองระบบทันที"),
    ]
    add_table(doc, ["หัวข้อ", "ข้อกำหนด"], security_rows, [2300, 7060], font_size=9.2)

    add_page_break(doc)
    doc.add_heading("12. รายการข้อมูลที่ขอให้ eDOC ยืนยัน", level=1)
    confirmation_rows = [
        ("1", "DEV/UAT Base URL", "https://edoc.railway.co.th/RwSarabanApiDev/", "□ ยืนยัน  □ แก้ไข"),
        ("2", "Production Base URL", "____________________________", "□ ส่งภายหลัง UAT"),
        ("3", "Service Account", "ส่งผ่านช่องทางลับ ไม่กรอกในเอกสาร", "□ ออกใหม่แล้ว"),
        ("4", "AppId", "____________________________", "□ ยืนยัน"),
        ("5", "SarabanUsername", "ส่งผ่านช่องทางที่ตกลง", "□ ยืนยัน"),
        ("6", "BasketID + ชื่อฝ่าย", "แนบรายการรหัส 13 หลัก", "□ แนบแล้ว"),
        ("7", "Public IP allowlist", "จำเป็น / ไม่จำเป็น", "□ ยืนยัน"),
        ("8", "WID uniqueness/idempotency", "WID เดิม create ซ้ำได้หรือไม่", "□ ยืนยัน"),
        ("9", "File types / max size", "pdf/jpg/docx และ ______ MB", "□ ยืนยัน"),
        ("10", "Wstype / ReceiveDoc", "required หรือ optional; code ที่รองรับ", "□ ยืนยัน"),
        ("11", "Priority / SecretLevel code", "00-03 และ 0 ตามที่เข้าใจ", "□ ยืนยัน"),
        ("12", "Tracking status code", "ความหมาย 00/02/03/09", "□ ส่งคำอธิบาย"),
        ("13", "Rate limit / maintenance window", "____________________________", "□ ยืนยัน"),
        ("14", "Response schema/error codes", "แนบตัวอย่าง success/error จริง", "□ แนบแล้ว"),
    ]
    add_table(doc, ["ลำดับ", "รายการ", "ค่าที่ต้องยืนยัน", "สถานะ"], confirmation_rows, [900, 2600, 3800, 2060], font_size=8.8)

    doc.add_heading("12.1 ผู้ประสานงานและการรับรองผล", level=2)
    signoff_rows = [
        ("SRT - ผู้ประสานงาน", "ชื่อ __________________  โทร/อีเมล __________________  วันที่ __________"),
        ("eDOC - ผู้ประสานงาน", "ชื่อ __________________  โทร/อีเมล __________________  วันที่ __________"),
        ("ผล DEV/UAT", "□ ผ่าน  □ ผ่านแบบมีเงื่อนไข  □ ไม่ผ่าน  หมายเหตุ __________________________"),
        ("อนุมัติ Production", "□ อนุมัติ  □ ยังไม่อนุมัติ  ผู้อนุมัติ __________________  วันที่ __________"),
    ]
    add_table(doc, ["รายการ", "รายละเอียด"], signoff_rows, [2500, 6860], font_size=10)

    # Apply the same page geometry and running furniture to every section.
    for idx, section in enumerate(doc.sections):
        section.page_width = Inches(8.5)
        section.page_height = Inches(11)
        section.top_margin = Inches(1)
        section.bottom_margin = Inches(1)
        section.left_margin = Inches(1)
        section.right_margin = Inches(1)
        section.header_distance = Inches(0.492)
        section.footer_distance = Inches(0.492)
        set_header_footer(section, first=idx == 0)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(OUTPUT)
    print(OUTPUT)


if __name__ == "__main__":
    build_document()
