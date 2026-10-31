"""Reads files out of CS2's VPK archives (pak01_dir.vpk and its pak01_NNN.vpk parts). Standard library only."""
import pathlib
import struct


class Vpk:
    def __init__(self, dir_path):
        self.path = pathlib.Path(dir_path)
        self.data = self.path.read_bytes()
        signature, version, tree_size = struct.unpack_from("<III", self.data, 0)
        if signature != 0x55AA1234:
            raise ValueError(f"{self.path} is not a VPK")
        self.header = 28 if version == 2 else 12
        self.tree_end = self.header + tree_size
        self.entries = {}
        pos = self.header

        def cstr():
            nonlocal pos
            stop = self.data.index(b"\0", pos)
            text = self.data[pos:stop].decode("utf-8")
            pos = stop + 1
            return text

        while (ext := cstr()):
            while (folder := cstr()):
                while (name := cstr()):
                    _crc, preload, archive, offset, length = struct.unpack_from("<IHHII", self.data, pos)
                    pos += 18
                    full = f"{name}.{ext}" if folder.strip() == "" else f"{folder}/{name}.{ext}"
                    self.entries[full.lower()] = (archive, offset, length, self.data[pos:pos + preload])
                    pos += preload

    def __contains__(self, path):
        return path.lower() in self.entries

    def read(self, path):
        archive, offset, length, preload = self.entries[path.lower()]
        if archive == 0x7FFF:
            start = self.tree_end + offset
            return preload + self.data[start:start + length]
        part = self.path.with_name(self.path.name.replace("_dir", f"_{archive:03d}"))
        with open(part, "rb") as handle:
            handle.seek(offset)
            return preload + handle.read(length)

    def svg(self, path):
        """The SVG source inside a compiled .vsvg_c: CS2 stores it as plain text in the DATA block."""
        compiled = self.read(path)
        start, stop = compiled.find(b"<?xml"), compiled.rfind(b"</svg>")
        if start < 0 or stop < 0:
            raise ValueError(f"{path}: no SVG text inside")
        return compiled[start:stop + 6].decode("utf-8")
