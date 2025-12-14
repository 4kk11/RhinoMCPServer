using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class GetGeometryInfoTool : IMCPTool
    {
        public string Name => "get_geometry_info";
        public string Description => "Gets the geometric information of multiple Rhino objects.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guids": {
                        "type": "array",
                        "items": {
                            "type": "string"
                        },
                        "description": "Array of GUIDs of the target Rhino objects."
                    }
                },
                "required": ["guids"]
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpProtocolException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guids", out var guidsValue))
            {
                throw new McpProtocolException("Missing required argument: 'guids' is required");
            }

            var jsonElement = (JsonElement)guidsValue;
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                throw new McpProtocolException("The 'guids' argument must be an array");
            }

            var guidStrings = jsonElement.EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => x != null)
                .ToList();

            if (!guidStrings.Any())
            {
                throw new McpProtocolException("The guids array cannot be empty");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            var results = new List<object>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var guidString in guidStrings)
            {
                if (!Guid.TryParse(guidString, out Guid objectGuid))
                {
                    failureCount++;
                    results.Add(new { guid = guidString, status = "failure", reason = "Invalid GUID format" });
                    continue;
                }

                var rhinoObject = rhinoDoc.Objects.Find(objectGuid);
                if (rhinoObject == null)
                {
                    failureCount++;
                    results.Add(new { guid = guidString, status = "failure", reason = $"No object found with GUID: {objectGuid}" });
                    continue;
                }

                var geometry = rhinoObject.Geometry;
                if (geometry == null)
                {
                    failureCount++;
                    results.Add(new { guid = guidString, status = "failure", reason = "Object has no geometry" });
                    continue;
                }

                try
                {
                    var geometryInfo = GetGeometryInfo(geometry);
                    successCount++;
                    results.Add(new {
                        guid = guidString,
                        status = "success",
                        geometry = geometryInfo
                    });
                }
                catch (Exception ex)
                {
                    failureCount++;
                    results.Add(new {
                        guid = guidString,
                        status = "failure",
                        reason = $"Error getting geometry info: {ex.Message}"
                    });
                }
            }

            var response = new
            {
                summary = new
                {
                    totalObjects = guidStrings.Count,
                    successfulReads = successCount,
                    failedReads = failureCount
                },
                results = results
            };

            return Task.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }) }]
            });
        }

        private object GetGeometryInfo(GeometryBase geometry)
        {
            object geometryInfo;
            
            if (geometry is Curve curve)
            {
                if (curve.TryGetPolyline(out Polyline polyline))
                {
                    geometryInfo = new
                    {
                        type = "polyline",
                        points = polyline.Select(p => new { x = p.X, y = p.Y, z = p.Z }).ToArray(),
                        length = polyline.Length,
                        is_closed = polyline.IsClosed,
                        segment_count = polyline.SegmentCount,
                        point_count = polyline.Count
                    };
                }
                else if (curve.IsArc() && curve.TryGetArc(out Arc arc))
                {
                    var circle = new Circle(arc.Plane, arc.Radius);
                    geometryInfo = new
                    {
                        type = "circle",
                        center = new { x = circle.Center.X, y = circle.Center.Y, z = circle.Center.Z },
                        radius = circle.Radius,
                        circumference = circle.Circumference,
                        diameter = circle.Diameter,
                        plane = new {
                            origin = new { x = circle.Plane.Origin.X, y = circle.Plane.Origin.Y, z = circle.Plane.Origin.Z },
                            normal = new { x = circle.Plane.ZAxis.X, y = circle.Plane.ZAxis.Y, z = circle.Plane.ZAxis.Z }
                        }
                    };
                }
                else
                {
                    // その他の曲線タイプの場合は基本的な情報を返す
                    geometryInfo = new
                    {
                        type = "curve",
                        length = curve.GetLength(),
                        is_closed = curve.IsClosed,
                        domain = new { 
                            start = curve.Domain.Min,
                            end = curve.Domain.Max
                        },
                        start_point = new {
                            x = curve.PointAtStart.X,
                            y = curve.PointAtStart.Y,
                            z = curve.PointAtStart.Z
                        },
                        end_point = new {
                            x = curve.PointAtEnd.X,
                            y = curve.PointAtEnd.Y,
                            z = curve.PointAtEnd.Z
                        }
                    };
                }
            }
            else if (geometry is Surface surface)
            {
                var bbox = surface.GetBoundingBox(true);
                var domain_u = surface.Domain(0);
                var domain_v = surface.Domain(1);

                geometryInfo = new
                {
                    type = "surface",
                    is_periodic = surface.IsPeriodic(0) || surface.IsPeriodic(1),
                    is_singular = surface.IsSingular(0) || surface.IsSingular(1),
                    domain = new {
                        u = new { min = domain_u.Min, max = domain_u.Max },
                        v = new { min = domain_v.Min, max = domain_v.Max }
                    },
                    bounding_box = new
                    {
                        min = new { x = bbox.Min.X, y = bbox.Min.Y, z = bbox.Min.Z },
                        max = new { x = bbox.Max.X, y = bbox.Max.Y, z = bbox.Max.Z }
                    }
                };
            }
            else if (geometry is Brep brep)
            {
                var bbox = brep.GetBoundingBox(true);
                geometryInfo = new
                {
                    type = "brep",
                    face_count = brep.Faces.Count,
                    edge_count = brep.Edges.Count,
                    vertex_count = brep.Vertices.Count,
                    is_solid = brep.IsSolid,
                    is_manifold = brep.IsManifold,
                    area = AreaMassProperties.Compute(brep)?.Area ?? 0,
                    volume = brep.IsSolid ? VolumeMassProperties.Compute(brep)?.Volume ?? 0 : 0,
                    bounding_box = new
                    {
                        min = new { x = bbox.Min.X, y = bbox.Min.Y, z = bbox.Min.Z },
                        max = new { x = bbox.Max.X, y = bbox.Max.Y, z = bbox.Max.Z }
                    }
                };
            }
            else
            {
                // その他のジオメトリタイプの場合は基本的な情報を返す
                var bbox = geometry.GetBoundingBox(true);
                geometryInfo = new
                {
                    type = geometry.GetType().Name,
                    bounding_box = new
                    {
                        min = new { x = bbox.Min.X, y = bbox.Min.Y, z = bbox.Min.Z },
                        max = new { x = bbox.Max.X, y = bbox.Max.Y, z = bbox.Max.Z }
                    }
                };
            }

            return geometryInfo;
        }
    }
}