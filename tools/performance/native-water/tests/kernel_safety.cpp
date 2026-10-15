#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cstdint>
#include "../WaterKernel.cpp"

static unsigned checks = 0;
static void require(bool result, const char* description) {
    ++checks;
    if (!result) { std::fprintf(stderr, "FAIL: %s\n", description); std::exit(1); }
}
static bool same(const V* a, const V* b, size_t count) {
    return std::memcmp(a, b, count * sizeof(V)) == 0;
}
static uint32_t bits(float value) {
    uint32_t result;
    std::memcpy(&result, &value, sizeof(result));
    return result;
}
static void rejection_checks(int mode) {
    V vertices[] = {{0, 0, 0}, {1, 0, 0}, {0, 1, 0}, {2, 2, 2}};
    V normals[] = {{11, 12, 13}, {21, 22, 23}, {31, 32, 33}, {41, 42, 43}};
    V original_vertices[4], original_normals[4];
    std::memcpy(original_vertices, vertices, sizeof(vertices));
    std::memcpy(original_normals, normals, sizeof(normals));
    const int32_t triangles[] = {0, 1, 2, 0, 2, 3};
    // The first triangle is valid. A bad later index must still reject before its writes.
    const int32_t out_of_range[] = {0, 1, 2, 0, 2, 4};
    const int32_t negative[] = {0, 1, 2, 0, -1, 3};
    require(ftk_normals(vertices, 4, normals, 4, out_of_range, 6, mode) == 0, "out-of-range index rejected");
    require(same(normals, original_normals, 4), "out-of-range rejection did not write earlier triangle");
    require(ftk_normals(vertices, 4, normals, 4, negative, 6, mode) == 0, "negative index rejected");
    require(same(normals, original_normals, 4), "negative-index rejection preserved output");
    const int counts[] = {-3, -1, 1, 2, 4, 5};
    for (int count : counts) {
        require(ftk_normals(vertices, 4, normals, 4, triangles, count, mode) == 0, "invalid triangle count rejected");
        require(same(normals, original_normals, 4), "invalid triangle count preserved output");
    }
    require(ftk_normals(vertices, 0, normals, 0, triangles, 6, mode) == 0, "zero vertices rejected");
    require(ftk_normals(vertices, -1, normals, -1, triangles, 6, mode) == 0, "negative vertex count rejected");
    require(ftk_normals(vertices, 4, normals, 3, triangles, 6, mode) == 0, "mismatched normal count rejected");
    require(ftk_normals(vertices, 4, normals, -1, triangles, 6, mode) == 0, "negative normal count rejected");
    require(ftk_normals(nullptr, 4, normals, 4, triangles, 6, mode) == 0, "null vertices rejected");
    require(ftk_normals(vertices, 4, nullptr, 4, triangles, 6, mode) == 0, "null normals rejected");
    require(ftk_normals(vertices, 4, normals, 4, nullptr, 6, mode) == 0, "null triangles rejected");
    require(ftk_normals(vertices, 4, vertices, 4, triangles, 6, mode) == 0, "identical input/output buffer rejected");
    require(same(normals, original_normals, 4), "all shape/null rejections preserved normals");
    require(same(vertices, original_vertices, 4), "all rejections preserved vertices");
    require(ftk_normals(vertices, 4, normals, 4, triangles, 0, mode) == 1, "empty topology accepted as a no-op");
    require(same(normals, original_normals, 4), "empty topology preserved all normals");
}
static void output_checks(int mode) {
    V vertices[] = {{0, 0, 0}, {1, 0, 0}, {0, 1, 0}, {123, 456, 789}};
    V normals[] = {{11, 12, 13}, {21, 22, 23}, {31, 32, 33}, {41, 42, 43}};
    V unused = normals[3];
    const int32_t forward[] = {0, 1, 2};
    const int32_t reverse[] = {0, 2, 1};
    const int32_t repeated[] = {0, 0, 0};
    require(ftk_normals(vertices, 4, normals, 4, forward, 3, mode) == 1, "valid triangle accepted");
    for (int i = 0; i < 3; ++i)
        require(normals[i].x == 0 && normals[i].y == 0 && normals[i].z == 1, "forward triangle orientation");
    require(same(&normals[3], &unused, 1), "unreferenced normal retained bitwise");
    require(ftk_normals(vertices, 4, normals, 4, reverse, 3, mode) == 1, "reverse triangle accepted");
    for (int i = 0; i < 3; ++i)
        require(normals[i].x == 0 && normals[i].y == 0 && normals[i].z == -1, "reverse triangle orientation");
    V untouched[] = {normals[1], normals[2], normals[3]};
    require(ftk_normals(vertices, 4, normals, 4, repeated, 3, mode) == 1, "repeated-index degenerate triangle accepted");
    require(bits(normals[0].x) == 0x80000000u && bits(normals[0].y) == 0x80000000u && bits(normals[0].z) == 0x80000000u,
        "degenerate normal retains negative zero components");
    require(same(&normals[1], untouched, 3), "degenerate triangle preserves all unused slots");
}
static void unbound_water_check() {
    V vertices[] = {{0, 7, 0}, {1, 8, 0}, {0, 9, 1}};
    V normals[] = {{11, 12, 13}, {21, 22, 23}, {31, 32, 33}};
    V original_vertices[3], original_normals[3];
    std::memcpy(original_vertices, vertices, sizeof(vertices));
    std::memcpy(original_normals, normals, sizeof(normals));
    const int32_t triangles[] = {0, 1, 2};
    require(ftk_water(vertices, 3, normals, 3, triangles, 3, 0.5f, 2.0f, 3) == 0, "unbound Unity Perlin rejects full water batch");
    require(same(vertices, original_vertices, 3), "unbound full batch preserved vertex heights");
    require(same(normals, original_normals, 3), "unbound full batch preserved normals");
}
int main() {
    for (int mode = 0; mode < 4; ++mode) { rejection_checks(mode); output_checks(mode); }
    unbound_water_check();
    std::printf("Native kernel boundary checks passed: %u (host architecture; no Unity interop claim).\n", checks);
}
