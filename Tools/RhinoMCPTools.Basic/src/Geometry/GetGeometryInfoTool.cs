using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using ModelContextProtocol.Protocol.Types;
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
        public string Description => "Gets the geometric information of a Rhino object.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target Rhino object."
                    }
                },
                "required": ["guid"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue))
            {
                throw new McpServerException("Missing required argument: 'guid' is required");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (!Guid.TryParse(guidValue.ToString(), out Guid objectGuid))
            {
                throw new McpServerException("Invalid GUID format");
            }

            var rhinoObject = rhinoDoc.Objects.Find(objectGuid);
            if (rhinoObject == null)
            {
                throw new McpServerException($"No object found with GUID: {objectGuid}");
            }

            var geometry = rhinoObject.Geometry;
            if (geometry == null)
            {
                throw new McpServerException("Object has no geometry");
            }

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

            var response = new
            {
                status = "success",
                geometry = geometryInfo
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}